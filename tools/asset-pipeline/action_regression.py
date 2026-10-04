#!/usr/bin/env python3
"""action_regression -- do two traced films perform the same fight and the same authored actions? (ADR-011)

    python tools/asset-pipeline/action_regression.py <a.log> <b.log> [--refs] [--also KIND ...]

Compares, on the fight's own playhead (wall time is ignored: a longer wave-clear break shifts every later frame without
changing a single beat):
  - the fight: every `event` line (kind, slot, amount, ms, crit), in order: the same dice must roll the same fight;
  - the ACTIONS: every clip-start, release, contact, contact-tick, handoff, yield, clip-end and root line, and every
    sound an authored action voices (sfx_seeker_spray_*, sfx_seeker_hard_hands_*), keyed by the MOMENT
    (playhead, kind, subject). A moment both films traced must say the same thing.
  - with --refs (the remaining-skill sweep's Phase 0 baseline, 2026-10-03): ALL FIVE closed Seeker references, not
    only the two actions. The compared set grows by
      * every `reaction-*` line (JAWS: spawn, clamp, number, cue, end, draw), `reaction-draw` minus its `draws=` field;
      * `field-wave`, `field-draw`, `field-cue` (PRESS);
      * `mark-wave`, `mark-draw`, `mark-cue` (BRAND), minus `draws=` and `batches=`;
      * every sound whose key starts with sfx_seeker_jaws / sfx_seeker_press / sfx_seeker_brand.
      * `reaction-clamp belt=` held by its y only (P1.3: its x is the champion's draw push, see CHAMP_X);
    `draws=` / `batches=` are UPPER BOUNDS that include the generic effects pass (puffs, the heal column), which Phase 0
    deliberately changes; they are dropped from the reference lines, never the counts of the reference's own sprites.
    `callout`, `flash`, `number`, `vfx-spawn` and generic `sound` lines are NOT reference lines: Phase 0 changes them
    on purpose (SPRAY's callout becomes SPRAY, generic thuds collapse to one per Strike batch).
    Fields that differ between two runs of the SAME build (A/A) are dropped too; each is named in UNSTABLE below.

A DRAW-SAMPLED line (`root`, and every `*-draw` kind) at a moment only ONE film has is reported and not counted: those
lines are written by Draw, and a catch-up tick (a slow frame while a PNG is written) skips a Draw, so one film may simply
not have sampled that frame; for the same reason a Draw-sampled line REPEATED at one playhead counts once under --refs.
Since P0.2 (--refs): a Draw-sampled list at one moment that is the other film's with samples left out is a skipped Draw;
a persistent layer's Draw lines (field-draw, mark-draw) at a FROZEN final playhead are wall-clock sampling; field-draw's
body x and alloc>0 moment are cold-start sampling (HOST_X, PER_FILM_FLAG below). All reported, none counted.
A moment both films sampled must still say the same thing. Any other moment one film traced
and the other did not, inside the span both cover, is a difference.
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

# --refs: the reaction / field / mark traces and the three later references' voices
REF_KINDS = {"field-wave", "field-draw", "field-cue", "mark-wave", "mark-draw", "mark-cue"}
REF_PREFIXES = ("reaction-",)
REF_VOICES = VOICES + ("sfx_seeker_jaws", "sfx_seeker_press", "sfx_seeker_brand")
# upper bounds that include the generic effects pass, which Phase 0 changes on purpose
DROPPED = {"reaction-draw": ("draws",), "mark-draw": ("draws", "batches")}
# fields that differ A/A (two runs of the same build and seed, ae20a7fd, production/qa/evidence/vfx-sweep/phase0-baseline):
#   mark-draw ticks= / flush=: Stopwatch ticks spent in the curse's draw and its flush -- wall-clock CPU time;
#   field-draw shape=: PRESS's front / arc rectangle is LATCHED in Draw from the target's pose on the first drawn frame
#                      after the launch, so a catch-up tick that skips that Draw latches it a frame later (ref_fast A/A:
#                      1056,700,340,184 vs 1109,688,225,196 at the same playhead; every other field-draw field equal).
UNSTABLE: dict[str, tuple[str, ...]] = {"mark-draw": ("ticks", "flush"), "field-draw": ("shape",)}
# fields held as ZERO / NON-ZERO only: mark-draw alloc= is 0 on every steady frame A/A, but the FIRST curse frame's bytes
# include the runtime's own lazy work (25752 vs 1128 in ref_brand A/A); field-draw alloc= is a running total for the
# wave that jumps by 8-14 MB on a few frames (a first-use load inside the measured window, landing on whichever frame
# Draw sampled: ref_fast A/A 13107864 vs 48 at 5717) and is 0 / 48 otherwise. The zero / non-zero claim stays held.
FLAGGED: dict[str, tuple[str, ...]] = {"mark-draw": ("alloc",), "field-draw": ("alloc",)}


def is_ref(kind: str) -> bool:
    return kind in REF_KINDS or kind.startswith(REF_PREFIXES)


def is_draw_sampled(kind: str) -> bool:
    return kind == "root" or kind.endswith("-draw")


# P0.2 (2026-10-03): the first ~250 ms of a take's playhead run while the process is still cold, and the catch-up
# pattern there is the MACHINE's (ref_fast re-filmed through RH_SHOT_BUILD: the same fight, event for event, while two
# Draw-sampled field-draw fields moved). So, under --refs:
#   field-draw body= keeps its y, w, h and drops x: x carries the host creature's bite lunge (HuntScreen.LungePx, read from
#     the windup timer advanced by frame dt), 1118 vs 1121 at one playhead with enemy-root x=6 vs 9 there;
#   field-draw alloc= is held per FILM, not per moment: the count of moments with alloc > 0 inside the span both films
#     cover must match (the 8-14 MB first-use jump landed at 3700 in one film and 3783 in the other).
HOST_X = {"field-draw": "body"}
PER_FILM_FLAG = {"field-draw": "alloc"}
# P1.3 (2026-10-04): reaction-clamp belt= keeps its y and drops its x. belt is the CHAMPION's drawn belt point, read from
# his published body, and only the RH_SHOT_SOCKETS overlay draws it (JAWS' teeth, clamp, ring and number never read it).
# Its x carries the champion's draw push, which P1.3 changes on purpose: the baseline's legacy 40 px post-hit lunge
# (ref_fast 7333: belt=687 = 647 + 40, the swing landed at 7300) is the performed swing's step-in now (756 = 647 + 109).
# clamp= and body= (the creature's) are held whole.
CHAMP_X = {"reaction-clamp": "belt"}


def strip_fields(kind: str, fields: list[str]) -> list[str]:
    if kind in HOST_X:
        fields = [f"{HOST_X[kind]}=~,{f.partition('=')[2].partition(',')[2]}" if f.startswith(HOST_X[kind] + "=") else f
                  for f in fields]
    if kind in CHAMP_X:
        fields = [f"{CHAMP_X[kind]}=~,{f.partition('=')[2].partition(',')[2]}" if f.startswith(CHAMP_X[kind] + "=") else f
                  for f in fields]
    drop = DROPPED.get(kind, ()) + UNSTABLE.get(kind, ())
    flag = FLAGGED.get(kind, ())
    if not drop and not flag:
        return fields
    out = []
    for f in fields:
        name, _, value = f.partition("=")
        if name in drop:
            continue
        out.append(f"{name}={'0' if value == '0' else '>0'}" if name in flag else f)
    return out


def lines(path: str, extra: set[str], refs: bool) -> tuple[list[tuple], list[tuple]]:
    fight, action = [], []
    voices = REF_VOICES if refs else VOICES
    for raw in open(path, encoding="utf-8", errors="replace"):
        p = raw.rstrip("\n").split(TAB)
        if len(p) < 4 or p[0] != "present":
            continue
        kind = p[3]
        if kind == "event":
            fight.append((p[2], TAB.join(p[4:])))
        elif kind in ACTION or kind in extra or (kind == "sound" and len(p) > 4 and p[4].startswith(voices)):
            action.append((p[2], kind, TAB.join(p[4:])))
        elif refs and is_ref(kind):
            action.append((p[2], kind, TAB.join(strip_fields(kind, p[4:]))))
    return fight, action


def compare_fight(a: list[tuple], b: list[tuple]) -> int:
    n = min(len(a), len(b))
    diff = [i for i in range(n) if a[i] != b[i]]
    print(f"fight    a={len(a):5}  b={len(b):5}  compared in order={n:5}  differing={len(diff)}")
    for i in diff[:4]:
        print(f"           a {a[i]}")
        print(f"           b {b[i]}")
    return len(diff)


def subject_of(kind: str, rest: str, refs: bool) -> str:
    if kind == "root":
        return ""
    first = rest.split(TAB)[0]
    # a reference line whose first field is a measurement (field-draw u=..., reaction-draw alive=...) has no subject:
    # keying on it would turn a changed value into "a moment in one film only" instead of a difference
    if refs and is_ref(kind) and "=" in first:
        return ""
    return first


def is_subsequence(a: list[str], b: list[str]) -> bool:
    """Is the shorter list the longer one with some entries left out (order kept)?"""
    short, long_ = (a, b) if len(a) <= len(b) else (b, a)
    it = iter(long_)
    return all(any(x == y for y in it) for x in short)


def compare_actions(a: list[tuple], b: list[tuple], refs: bool) -> int:
    def keyed(rows):
        d: dict[tuple, list[str]] = {}
        for ph, kind, rest in rows:
            got = d.setdefault((ph, kind, subject_of(kind, rest, refs)), [])
            # under --refs a Draw-sampled line repeated at one playhead (a Draw with no tick between, e.g. after the fight
            # froze its playhead) is one sample: a Draw can be repeated as well as skipped
            if not (refs and is_draw_sampled(kind) and rest in got):
                got.append(rest)
        return d

    # the per-film claims (PER_FILM_FLAG): taken out of each line and counted per film inside the span both cover
    end_all = min(max((float(r[0]) for r in a), default=0.0), max((float(r[0]) for r in b), default=0.0))
    film_flags = []
    if refs:
        def split(rows):
            out, flagged = [], set()
            for ph, kind, rest in rows:
                name = PER_FILM_FLAG.get(kind)
                if name:
                    kept = [f for f in rest.split(TAB) if not f.startswith(name + "=")]
                    if any(f == name + "=>0" for f in rest.split(TAB)) and float(ph) <= end_all:
                        flagged.add((ph, kind))
                    rest = TAB.join(kept)
                out.append((ph, kind, rest))
            return out, flagged
        a, fa = split(a)
        b, fb = split(b)
        for kind, name in PER_FILM_FLAG.items():
            na = sum(1 for _, k in fa if k == kind)
            nb = sum(1 for _, k in fb if k == kind)
            print(f"           {kind} {name}>0 moments (per film): a={na}  b={nb}")
            if na != nb:
                film_flags.append(kind)
    ka, kb = keyed(a), keyed(b)
    common = [k for k in ka if k in kb]
    diff = [k for k in common if ka[k] != kb[k]]
    # under --refs, Draw-sampled lines at one moment whose shorter list is the longer one with samples left out are a
    # SKIPPED Draw, not a difference (P0.2: after ref_jaws_kill's playhead freezes, PRESS's front keeps drawing on frames,
    # and one film drew one frame more before the cut; every value both films drew is the same, in the same order)
    skipped = [k for k in diff if refs and is_draw_sampled(k[1]) and is_subsequence(ka[k], kb[k])]
    # ...and at a FROZEN final playhead (the fight ended: ref_jaws_kill's killing answer) a persistent layer's Draw line is
    # wall-clock sampling: the playhead stands still while frames go on drawing, so which frames each film drew there is
    # the machine's, not the game's (P0.2 measured it differing between two runs of the unchanged default fixture)
    last_a = max((float(k[0]) for k in ka), default=0.0)
    last_b = max((float(k[0]) for k in kb), default=0.0)
    frozen = [k for k in diff if refs and k[1] in ("field-draw", "mark-draw") and float(k[0]) == last_a == last_b
              and len(ka[k]) > 1 and len(kb[k]) > 1]
    skipped += [k for k in frozen if k not in skipped]
    diff = [k for k in diff if k not in skipped]
    # a moment one film traced and the other did not, inside the span both cover, is a real difference -- unless Draw
    # wrote it (root; under --refs every *-draw): a catch-up tick skips a Draw. The default keeps the old rule (root only).
    end = min(max((float(k[0]) for k in ka), default=0.0), max((float(k[0]) for k in kb), default=0.0))
    one = [k for k in set(ka) ^ set(kb) if float(k[0]) <= end]
    real = [k for k in one if not (is_draw_sampled(k[1]) if refs else k[1] == "root")]
    sampled = len(one) - len(real) + len(skipped)
    label = "draw samples" if refs else "root samples"
    print(f"{'refs' if refs else 'actions'}  a={len(a):5}  b={len(b):5}  moments both traced={len(common):5}  differing={len(diff)}"
          f"  {label} in one film only={sampled}  other moments in one film only={len(real)}")
    if refs:
        kinds: dict[str, int] = {}
        for _, kind, _ in a:
            kinds[kind] = kinds.get(kind, 0) + 1
        print("           a lines by kind: " + ", ".join(f"{k}={v}" for k, v in sorted(kinds.items())))
    for k in sorted(diff, key=lambda k: float(k[0]))[:6]:
        print(f"           {k}: a {ka[k]}  b {kb[k]}")
    for k in sorted(real, key=lambda k: float(k[0]))[:6]:
        print(f"           only in {'a' if k in ka else 'b'}: {k}")
    return len(diff) + len(real) + len(film_flags)


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("a")
    ap.add_argument("b")
    ap.add_argument("--refs", action="store_true",
                    help="hold all five Seeker references equal: + reaction-*, field-*, mark-* and their voices")
    ap.add_argument("--also", nargs="*", default=[], help="more trace kinds to hold equal")
    args = ap.parse_args()
    fa, aa = lines(args.a, set(args.also), args.refs)
    fb, ab = lines(args.b, set(args.also), args.refs)
    bad = compare_fight(fa, fb) + compare_actions(aa, ab, args.refs)
    print("IDENTICAL" if bad == 0 else f"{bad} DIFFERENCES")
    return 1 if bad else 0


if __name__ == "__main__":
    sys.exit(main())
