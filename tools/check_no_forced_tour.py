#!/usr/bin/env python3
"""NO TOUR MAY START ITSELF. A full-screen tour is asked for, or it does not happen.

    python3 tools/check_no_forced_tour.py

WHY THIS IS A GATE. The first thing this game used to do to a new player was interrupt it. A fresh
save opened on a fight and, seconds later, put an eight-card modal over it — the hunter, the enemies,
the health bar, Gleam, the skills, the rewards, the rail, the lesson slot — before a single wave had
resolved. Every other screen then did the same on its first open: four cards before you could equip
anything, four before you could upgrade anything. Nothing was learned, because nothing had happened
yet; the cards were a manual for a game the reader had not played.

The redesign kept every one of those cards and changed who asks for them. They live behind LEARN THIS
SCREEN — the ? beside the settings gear — and the coach says one short thing at a time from real
state instead. That is a behaviour, and a behaviour with no test is a behaviour that comes back: the
auto-start was three lines in Update, and three lines is exactly the size of a change somebody makes
while fixing something else.

WHAT IT CHECKS. Every call to BeginTour in Game1.cs is one of:

  * inside `CaptureRig` — the shutter poses a tour by name, which is how the copy is photographed;
  * inside a click handler — the player asked (TakeLearnClick, and the tour's own next-card path).

A call that is neither is an automatic tour, and this fails naming its line.

WHAT IT DOES NOT CHECK. Whether the copy is any good, or whether the ? is reachable on a given
screen — a Game test covers the second (onboarding_targets_test.cs). This gate is about consent.
"""
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
GAME1 = os.path.join(ROOT, "src", "IdleXIdle.Game", "Game1.cs")

# A call site is allowed when the enclosing method is one of these, or the call's own line is
# guarded by CaptureRig. Each name is a place the player has already asked, or a rig.
ASKED = {
    "TakeLearnClick",   # the ? beside the gear — the one way a tour starts in play
    "BeginTour",        # the two overloads call each other
}


HEAD = re.compile(r"\b(if|while|for|foreach|switch)\s*\(")


def condition_above(lines, j):
    """The whole condition of the control statement that ends at (or opens on) line j."""
    out = []
    k = j
    while k >= 0 and k > j - 10:
        out.append(lines[k])
        if HEAD.search(lines[k]):
            break
        k -= 1
    return "\n".join(out), k


def rig_guarded(lines, i):
    """Is the BeginTour on line i inside a CaptureRig condition — braced or not?"""
    if "CaptureRig" in lines[i]:
        return True

    # A BRACELESS guard: the statement directly above is an `if (...)` whose body is this line.
    prev = i - 1
    while prev >= 0 and not lines[prev].strip():
        prev -= 1
    if prev >= 0 and lines[prev].rstrip().endswith(")"):
        cond, _ = condition_above(lines, prev)
        if HEAD.search(cond) and "CaptureRig" in cond:
            return True

    # BRACED: walk outwards, block by block, and read each opening statement's condition.
    depth, j = 0, i - 1
    while j >= 0:
        depth += lines[j].count("}") - lines[j].count("{")
        if depth < 0:
            cond, k = condition_above(lines, j)
            if "CaptureRig" in cond:
                return True
            depth, j = 0, k - 1
            continue
        j -= 1
    return False


def enclosing_method(lines, i):
    """The nearest method signature above line i, by brace-free heuristic on the declaration."""
    sig = re.compile(r"^\s{4}(?:\[[^\]]*\]\s*)?(?:private|internal|public|protected)[^;{]*\b(\w+)\s*\(")
    for j in range(i, -1, -1):
        m = sig.match(lines[j])
        if m:
            return m.group(1)
    return "?"


def main():
    with open(GAME1, encoding="utf-8-sig") as fh:
        lines = fh.read().splitlines()

    bad = []
    for i, line in enumerate(lines):
        if "BeginTour(" not in line:
            continue
        if re.search(r"\b(private|internal)\s+void\s+BeginTour\s*\(", line):
            continue                      # the declarations themselves
        if rig_guarded(lines, i):
            continue                      # posed for the shutter
        who = enclosing_method(lines, i)
        if who in ASKED:
            continue
        bad.append((i + 1, who, line.strip()))

    for ln, who, text in bad:
        print(f"Game1.cs:{ln}  a tour starts itself in {who}()\n    {text}")
    if bad:
        print(f"\n{len(bad)} automatic tour start(s). A tour is asked for (LEARN THIS SCREEN) or posed by the rig.")
        return 1
    print("no tour starts itself")
    return 0


if __name__ == "__main__":
    sys.exit(main())
