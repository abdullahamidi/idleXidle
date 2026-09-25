#!/usr/bin/env python3
"""reaction_timeline -- every enemy BITE in a traced film, and what the fight and the screen did about it (ADR-011).

    python tools/asset-pipeline/reaction_timeline.py <film.log> [--slot 3] [--detail]

A REACTION (JAWS; REPAY shares its trap Form but is an ACTIVE) takes no champion beat: it answers an enemy's bite. This lines up, per bite, on
the fight's playhead: the bite (EnemyStrike: its attacker slot and amount), the champion's damage, whether the
reaction answered (its Skill event) and its reflected Strike, every number, flash, effect and sound the screen
asked for within the phrase, what the champion's figure was doing at that instant (the committed clip and frame),
and whether a trap clip played. `--detail` prints every trace line in the phrase.

`--report` prints, for each trigger of the reaction, its whole life on the replay's playhead (the base JAWS slice,
2026-09-25): the enemy's wind-up start, the contact (the bite), the champion's damage, the trigger, the snap, the
reaction's sound, the reflected strike, its number and flash, the recoil's end, the world effect's end, the rearm's
start (the trigger) and its end (ReactionArmed, the fight's own report), and the next trigger.

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
    ap.add_argument("--report", action="store_true", help="each trigger's whole life, one line per moment")
    a = ap.parse_args()
    rows = parse(a.log)
    if a.report:
        return report(rows, a.slot)
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
            elif k2.startswith("reaction-") and k2 != "reaction-draw":
                answer.append(f"{k2}@{d}")
            elif k2 == "event" and f2[0] == "ReactionArmed" and kv(f2).get("slot") == str(a.slot):
                answer.append(f"ARMED(ran {kv(f2).get('amount')})@{d}")
        print(f"{k:>3} {bite:8.0f} {attacker:>4} {kv(f)['amount']:>4} {'yes' if trig is not None else 'no':>5} {since:>10} {doing:>18}  {' '.join(answer)}")
        if a.detail:
            for p2, k2, f2 in phrase:
                print(f"        {p2 - bite:+6.0f}  {k2:12} {' '.join(f2)}")
    return 0


def report(rows, slot: int) -> int:
    """Each trigger of the reaction in `slot`, one line per moment of its life, on the playhead."""
    s = str(slot)
    triggers = [i for i, r in enumerate(rows) if r[3] == "event" and r[4][0] == "Skill" and kv(r[4]).get("slot") == s]
    for n, i in enumerate(triggers):
        at = float(kv(rows[i][4])["at"])
        nxt = next((float(kv(rows[j][4])["at"]) for j in triggers[n + 1:]), None)
        lines = []

        def first(pred, start=0, stop=None, back=False):
            rng = range(i - 1, start - 1, -1) if back else range(i, stop if stop is not None else len(rows))
            for j in rng:
                if pred(rows[j]):
                    return rows[j]
            return None

        wind = first(lambda r: r[3] == "enemy-windup" and float(kv(r[4]).get("bite", "-1")) == at, back=True)
        bite = first(lambda r: r[3] == "event" and r[4][0] == "EnemyStrike" and float(kv(r[4])["at"]) == at, back=True)
        armed_before = first(lambda r: r[3] == "event" and r[4][0] == "ReactionArmed" and kv(r[4]).get("slot") == s, back=True)
        stop = next((j for j in triggers[n + 1:]), len(rows))

        def after(pred):
            return first(pred, stop=stop)

        def ms(r):
            return f"{r[2] - at:+.0f}" if r else "-"

        lines.append(("enemy wind-up start", ms(wind), f"bite={at:.0f}" if wind else "(before the log)"))
        lines.append(("contact (the bite)", ms(bite), f"at={at:.0f}"))
        lines.append(("champion damage", ms(bite), f"amount={kv(bite[4]).get('amount')}" if bite else ""))
        if armed_before:
            a_at = float(kv(armed_before[4])["at"])
            lines.append(("armed before it (window)", f"{a_at - at:+.0f}", f"ran={kv(armed_before[4]).get('amount')} ms; armed {at - a_at:.0f} ms before this bite"))
        lines.append(("JAWS trigger", ms(rows[i]), f"slot={s}"))
        spawn = after(lambda r: r[3] == "reaction-spawn")
        lines.append(("reaction spawn", ms(spawn), " ".join(spawn[4][1:]) if spawn else "(no authored reaction)"))
        snd = after(lambda r: r[3] == "sound" and "jaws" in r[4][0])
        lines.append(("reaction sound", ms(snd), snd[4][0] + " " + " ".join(snd[4][1:]) if snd else "-"))
        strike = after(lambda r: r[3] == "event" and r[4][0] == "Strike")
        lines.append(("reflected strike", ms(strike), f"creature={kv(strike[4]).get('slot')} amount={kv(strike[4]).get('amount')}" if strike else "-"))
        struck = kv(strike[4]).get("slot") if strike else None
        # the answer's own number and flash: on the struck creature, on the bite's frame (not a later blow's)
        num = after(lambda r: r[3] == "number" and kv(r[4]).get("slot") == struck and r[2] - at <= 60)
        lines.append(("damage number", ms(num), " ".join(num[4]) if num else "-"))
        fl = after(lambda r: r[3] == "flash" and kv(r[4]).get("slot") == struck and r[2] - at <= 60)
        lines.append(("target flash", ms(fl), " ".join(fl[4]) if fl else "-"))
        snap = after(lambda r: r[3] == "reaction-snap")
        lines.append(("jaws shut (snap)", ms(snap), " ".join(snap[4][2:]) if snap else "-"))
        lines.append(("recoil start", ms(snap), "(from the snap)" if snap else "-"))
        rend = after(lambda r: r[3] == "reaction-recoil-end")
        lines.append(("recoil end", ms(rend), ""))
        rel = after(lambda r: r[3] == "reaction-release")
        lines.append(("release", ms(rel), " ".join(rel[4][2:]) if rel else "-"))
        end = after(lambda r: r[3] == "reaction-end")
        lines.append(("world effect end", ms(end), " ".join(end[4][2:]) if end else "-"))
        lines.append(("rearm start", ms(rows[i]), "(the trigger)"))
        armed = after(lambda r: r[3] == "event" and r[4][0] == "ReactionArmed" and kv(r[4]).get("slot") == s)
        if armed is None and nxt is not None:
            armed = first(lambda r: r[3] == "event" and r[4][0] == "ReactionArmed" and kv(r[4]).get("slot") == s
                          and float(kv(r[4])["at"]) == nxt, stop=len(rows))
        lines.append(("ReactionArmed", f"{float(kv(armed[4])['at']) - at:+.0f}" if armed else "-",
                      f"at={kv(armed[4])['at']} ran={kv(armed[4])['amount']}" if armed else "(after the log)"))
        lines.append(("next trigger", f"{nxt - at:+.0f}" if nxt is not None else "-", ""))
        dup = [r for r in rows[i:stop] if r[3] == "sound" and r[4][0] in ("sfx_cast",)]
        print()
        print(f"== trigger {n + 1} at {at:.0f} ==" + ("   (!) sfx_cast still played" if dup else ""))
        for name, when, what in lines:
            print(f"   {name:26} {when:>7}  {what}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
