#!/usr/bin/env python3
"""Read an opening trace (RH_OPENING_TRACE) back and fail on the orders the 2026-09-11 pass fixed.

    py tools/check_opening_trace.py build/shots/opening_flow/100/trace.txt

The trace is one line per thing that happened, frame-stamped: "F <frame> <EVENT> <key=value ...>".
It is written by Game1.OpeningRig.cs while a synthetic hand plays a fresh career through the real
input path. Each check below is an ORDER a screenshot cannot prove:

  GLEAM      the first clear's final EnemyDown < its last fall finished <= the GLEAM card
             < the next wave's entrance, and the next wave only after CONTINUE
  SIGNATURE  the hold engaged <= the card < CONTINUE < the held cast crossed; the career's first
             cast IS the held one, it crossed exactly once, and nothing was drawn over it
             (HEALTH) before it had played
  GEAR       nothing was the player's pick before their click; the click picked the Welcome Gift;
             ITEM STATS came after the pick; EQUIP wore it; CONTINUE returned to the hunt
  EVERY STEP each clicked beat advanced on its first click, and nothing stalled
"""
import os
import re
import sys

WELCOME = "itm_gift_welcome_weapon"


def load(path):
    events = []
    with open(path, encoding="utf-8") as fh:
        for line in fh:
            m = re.match(r"F (\d+) (\S+)\s*(.*)$", line.strip())
            if m:
                events.append((int(m.group(1)), m.group(2), m.group(3)))
    return events


def kv(rest):
    return dict(part.split("=", 1) for part in rest.split() if "=" in part)


def main(path):
    ev = load(path)
    fails = []

    def check(ok, msg):
        print(("PASS  " if ok else "FAIL  ") + msg)
        if not ok:
            fails.append(msg)

    def first(kind, pred=lambda rest: True, after=-1):
        for frame, k, rest in ev:
            if k == kind and frame > after and pred(rest):
                return frame
        return None

    def stage(name):
        return first("STAGE", lambda r: r == name)

    def clicks(prefix, after=-1):
        return [f for f, k, r in ev if k == "CLICK" and r.startswith(prefix + " ") and f > after]

    # ── IT FINISHED ─────────────────────────────────────────────────────────────────────────────
    check(any(k == "COMPLETE" for _, k, _ in ev), "the opening reached Complete")
    stalls = [r for _, k, r in ev if k == "STALL"]
    check(not stalls, "no stage stalled" + (f" (stalled: {stalls[0]})" if stalls else ""))

    # ── GLEAM ───────────────────────────────────────────────────────────────────────────────────
    card = stage("IntroduceResources")
    kills = [f for f, k, _ in ev if k == "ENEMY_DOWN" and card is not None and f < card]
    kill = kills[-1] if kills else None
    falls = first("FALLS_PLAYED", after=kill - 1) if kill is not None else None
    ack = (clicks("IntroduceResources", card) or [None])[0] if card is not None else None
    nxt = first("WAVE_BEGIN", after=card) if card is not None else None
    traced = None not in (card, kill, falls, ack, nxt)
    check(traced, f"the first clear was traced (kill={kill} falls={falls} card={card} ack={ack} next={nxt})")
    if traced:
        check(kill < falls <= card,
              f"GLEAM waited for the last fall to finish (kill {kill} < fall done {falls} <= card {card})")
        check(card < nxt and ack < nxt,
              f"the next wave waited for GLEAM to be answered (card {card}, continue {ack}, next wave {nxt})")

    # ── THE BOSS: "A CHEST DROPPED" waits for the boss's fall, exactly as GLEAM waits for the first ──
    chest_card = stage("IntroduceChest")
    boss_kills = [f for f, k, _ in ev if k == "ENEMY_DOWN" and chest_card is not None and f < chest_card]
    boss_kill = boss_kills[-1] if boss_kills else None
    boss_falls = first("FALLS_PLAYED", after=boss_kill - 1) if boss_kill is not None else None
    check(None not in (chest_card, boss_kill, boss_falls) and boss_kill < boss_falls <= chest_card,
          f"A CHEST DROPPED waited for the boss's fall (kill {boss_kill} < fall done {boss_falls} <= card {chest_card})")

    # ── SIGNATURE ───────────────────────────────────────────────────────────────────────────────
    held = next(((f, kv(r)) for f, k, r in ev if k == "HELD" and kv(r).get("kind") == "Skill"), None)
    sig = stage("IntroduceSignature")
    sig_ack = (clicks("IntroduceSignature", sig) or [None])[0] if sig is not None else None
    casts = [(f, kv(r)) for f, k, r in ev if k == "SKILL"]
    check(held is not None and sig is not None and sig_ack is not None and bool(casts),
          f"the Signature beat was traced (held={held and held[0]} card={sig} continue={sig_ack} casts={len(casts)})")
    if held is not None and sig is not None and sig_ack is not None and casts:
        held_frame, held_kv = held
        at, wave = held_kv.get("at"), held_kv.get("wave")
        first_cast_frame, first_cast = casts[0]
        check(held_frame <= sig < sig_ack, f"hold {held_frame} <= card {sig} < continue {sig_ack}")
        # ...and the card froze no death under it: a creature still falling when the cast was parked
        # finished its fall before the words came up (GLEAM's rule, at the cast).
        pending = [f for f, k, _ in ev if k == "FALLS_PENDING" and f <= sig]
        if pending:
            fell = first("FALLS_PLAYED", after=pending[-1] - 1)
            check(fell is not None and fell <= sig,
                  f"the Signature card waited for the last fall in progress (pending {pending[-1]}, done {fell}, card {sig})")
        # SHORT OF THE WIND-UP, not one millisecond short of the event (the 2026-09-11 bug): the cast's
        # clip starts one contact-length (~780 ms at the opening's tempo) ahead of its event.
        lead = int(at) - int(float(held_kv.get("playhead", at))) if at is not None else 0
        check(lead >= 300, f"the hold parked ahead of the cast's wind-up (lead {lead} ms before the event)")
        check(first_cast_frame > sig_ack,
              f"no Signature cast was shown before CONTINUE (first cast at frame {first_cast_frame}, continue at {sig_ack})")
        check(first_cast.get("at") == at and first_cast.get("wave") == wave,
              f"the first cast of the career is the held one (held at={at} wave={wave}; "
              f"first cast at={first_cast.get('at')} wave={first_cast.get('wave')})")
        # ...counted inside the one wave the hold was in: from CONTINUE to that wave's end. A Hunter who
        # later falls and walks the waves again can cast at the same millisecond of a later wave two.
        wave_end = first("WAVE_BEGIN", after=sig_ack) or 10**9
        same = [f for f, c in casts if c.get("at") == at and sig_ack < f < wave_end]
        check(len(same) == 1, f"the held cast crossed exactly once ({len(same)} crossings before the next wave)")
        landed = first("RELEASED_PLAYED", after=sig_ack)
        health = stage("IntroduceHealth")
        check(landed is not None and health is not None and first_cast_frame <= landed <= health,
              f"nothing was drawn over the cast until it had played (cast {first_cast_frame}, "
              f"played {landed}, next card {health})")

    # ── GEAR ────────────────────────────────────────────────────────────────────────────────────
    sel = stage("ForceItemSelect")
    explain = stage("ExplainItem")
    equip = stage("ForceEquip")
    shown = stage("ShowEquipped")
    done = stage("Complete")
    pick_click = (clicks("ForceItemSelect", sel) or [None])[0] if sel is not None else None
    pick = first("GEAR_SELECTED", lambda r: kv(r).get("picked") == WELCOME, after=(sel or 0) - 1)
    check(None not in (sel, pick_click, pick, explain),
          f"the pick was traced (step={sel} click={pick_click} pick={pick} explain={explain})")
    if None not in (sel, pick_click, pick, explain):
        early = [r for f, k, r in ev if k == "GEAR_SELECTED" and sel <= f < pick_click and kv(r).get("picked") != "-"]
        check(not early, "nothing counted as the player's pick before their click" + (f" ({early[0]})" if early else ""))
        check(pick_click <= pick <= explain,
              f"the click picked the Welcome Gift and ITEM STATS followed (click {pick_click}, pick {pick}, card {explain})")
    equip_click = (clicks("ForceEquip", equip) or [None])[0] if equip is not None else None
    worn = first("WORN", lambda r: kv(r).get("n") == "1", after=(equip or 0) - 1)
    check(None not in (equip, equip_click, worn, shown) and equip_click <= worn <= shown,
          f"EQUIP wore the item and the step followed (click {equip_click}, worn {worn}, next {shown})")
    # ON or after: the last CONTINUE navigates in the same frame the cursor reaches Complete.
    back = first("SCREEN", lambda r: r == "Hunt", after=done - 1) if done is not None else None
    check(back is not None, f"CONTINUE on the last card returned to the hunt (complete {done}, hunt {back})")

    # ── NOTHING THE OPENING TAUGHT IS SAID AGAIN BY THE COACH ────────────────────────────────────
    repeats = [f"{r} (frame {f})" for f, k, r in ev if k == "LESSON" and r.split()[0] in ("SignatureSeen", "FirstBoss")]
    check(not repeats, "the coach repeated no lesson the opening taught" + (f" (said again: {repeats[0]})" if repeats else ""))
    # ...nor a notice toast: none drawn while the opening runs, and none for a screen it walked the player into.
    begin = stage("Prologue") or 0
    over = [f"{r} (frame {f})" for f, k, r in ev if k == "NOTICE" and begin <= f and (done is None or f < done)]
    check(not over, "no notice toast was drawn over the opening" + (f" (drawn: {over[0]})" if over else ""))
    stale = [f"{r} (frame {f})" for f, k, r in ev if k == "NOTICE" and ("NEW — VAULT" in r or "NEW — GEAR" in r)]
    check(not stale, "no notice repeated a screen the opening walked the player into" + (f" (said: {stale[0]})" if stale else ""))
    # ...NOR A DISPATCH, SURFACED. Background news goes to the inbox now, and the inbox may FILL all
    # through the opening — nothing about that is on screen. What must not happen is the news being
    # SURFACED: the envelope pulsing, the DISPATCHES lesson lighting, or the reading panel standing
    # open, each of which asks the player to look away from the thing they are being shown.
    mail = [f"{r} (frame {f})" for f, k, r in ev if k == "DISPATCH" and begin <= f and (done is None or f < done)]
    check(not mail, "no dispatch was surfaced over the opening" + (f" (surfaced: {mail[0]})" if mail else ""))
    # ...NOR THE RAIL, PERFORMING. Every gate the opening throws opens its tile at once -- that is game
    # truth and never waits -- but the CHAINS SPRINGING OFF are a ceremony on the far side of the page,
    # and one played while the player is reading an authored card is the game asking to be looked at in
    # two places. So each opened screen is latched and broken on the first frame the opening lets go:
    # after Complete, once per screen, never twice and never again on a later run.
    # Measured against the LAST AUTHORED CLICK, not against STAGE Complete: the stage is sampled at the
    # top of the following frame, so the frame the final CONTINUE frees the rail -- and the frame the
    # breaks land on -- is stamped one before it.
    # AND THE ANCHOR MUST EXIST. A run that recorded no CLICK at all has no last authored beat, and a
    # rule measured against nothing passes for the wrong reason -- "last card answered at frame None"
    # is the shape of a check that has stopped checking. A fresh career always answers cards, so no
    # anchor means the run did not play the opening, and that is a failure, not a pass.
    breaks = [(f, (r.split() or ["?"])[0]) for f, k, r in ev if k == "NAV_BREAK"]
    last_beat = max([f for f, k, _ in ev if k == "CLICK"], default=None)
    if last_beat is None:
        check(False, "the opening answered at least one authored card, so the rail has something to be "
                     "measured against (no CLICK in the trace -- the run never played the opening)")
    else:
        early = [f"{who} (frame {f})" for f, who in breaks if f < last_beat]
        check(not early, f"no rail tile performed its unlock while the opening still had the player "
                         f"(last card answered at frame {last_beat}, Complete at {done})"
              + (f" -- broke: {early[0]}" if early else ""))
    names = [who for _, who in breaks]
    twice = sorted({who for who in names if names.count(who) > 1})
    check(not twice, "every screen's chains came off exactly once"
          + (f" (again: {twice[0]})" if twice else ""))
    print("INFO  rail tiles broken after the opening let go: "
          + (", ".join(f"{who}@{f}" for f, who in breaks) or "none"))
    # AND THE TWO RULES ABOVE ARE NOT VACUOUS. With every background producer migrated, an opening run
    # normally records no NOTICE at all — so "no notice over the opening" would pass because nothing
    # could ever be traced, which is a check that has stopped checking. Both recorders are therefore
    # asserted to be WIRED, in the host, by name.
    for source, wire, what in (
        ("src/IdleXIdle.Game/Game1.cs", "_rigNoticeDrawn = _notice.Head;", "the notice toast"),
        ("src/IdleXIdle.Game/Game1.cs", '_rigDispatchSurfaced = "pulse";', "a surfaced dispatch"),
        ("src/IdleXIdle.Game/Game1.cs", 'OpeningRigMark($"NAV_BREAK {opened}"', "a rail tile's chains coming off"),
        ("src/IdleXIdle.Game/Game1.OpeningRig.cs", 'Trace($"DISPATCH {mail}")', "the DISPATCH trace event"),
    ):
        path = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), *source.split("/"))
        try:
            wired = wire in open(path, encoding="utf-8").read()
        except OSError:
            wired = False
        check(wired, f"the rig still records {what} (else the rules above pass vacuously)")
    # ...and no coach lesson at all while it runs: the opening is the one guide on screen.
    spoke = [f"{r} (frame {f})" for f, k, r in ev if k == "LESSON" and begin <= f and (done is None or f < done)]
    check(not spoke, "no coach lesson was shown over the opening" + (f" (shown: {spoke[0]})" if spoke else ""))

    # ── THE HUNTER'S OWN FALLS — not an order failure, but never silent ──────────────────────────
    downs = [(f, r) for f, k, r in ev if k == "HUNTER_DOWN"]
    for f, r in downs:
        print(f"WARN  the Hunter fell during the opening at frame {f} ({r}) and started the descent again")

    # ── EVERY CLICKED BEAT MOVED ON ITS FIRST CLICK — THE TITLE INCLUDED ────────────────────────
    # The title used to be exempt here, because it hit-tested its menu in DRAW: a press on a frame
    # MonoGame caught up with two Updates was latched by the first Update, erased by Latch() before the
    # second, and gone by the time the single Draw looked for it (seen once at 150 %, where the glyph
    # atlas is re-keyed and a frame can run over budget). The exemption was a known defect written into
    # the harness, so the harness could not fail on it. The hit test now lives in Update beside the
    # arrow keys (Game1's title branch), the edge is consumed in the same Update that latched it, and
    # the exemption is gone with it — a dropped title click fails this run.
    extra = [r for _, k, r in ev if k == "CLICK" and kv(r).get("n", "1") != "1"]
    check(not extra, "every clicked beat advanced on its first click" + (f" (again: {extra[0]})" if extra else ""))
    for r in [r for _, k, r in ev if k == "CLICK" and r.startswith("Title ") and kv(r).get("n", "1") != "1"]:
        print(f"WARN  the title took more than one click ({r})")

    print(f"{len(fails)} failed" if fails else "opening trace: every order holds")
    return 1 if fails else 0


if __name__ == "__main__":
    if len(sys.argv) != 2:
        print(__doc__)
        sys.exit(2)
    sys.exit(main(sys.argv[1]))
