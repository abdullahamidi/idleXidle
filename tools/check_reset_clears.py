#!/usr/bin/env python3
"""START A NEW GAME must clear every host field a loaded game writes.

    python3 tools/check_reset_clears.py

WHY THIS IS A GATE. Game1 keeps state that never reaches the save file — derived facts, one-shot
offers, the panel a return screen is holding. `LoadOrStartFresh` fills those from a save;
`StartNewGame` has to put every one of them back, by hand, because there is no object to replace.
Forty-odd assignments, and a field added to the loader is invisible to the reset.

It has already cost the player a real guarantee. The FREE OFFLINE RESUME is a host field: a reset
deleted the file, minted a world with no conquests and no depth, and the next descent still opened
at the wave the deleted account's absence had reached — because the resume's depth cap is applied
when a save LOADS, and a reset never loads one. Playtest 2026-09-09: "I reset the game but I died at
around wave 17; normally I have no way of getting there."

WHAT IT CHECKS. Every `_field = ...` assignment inside LoadOrStartFresh, against the fields
StartNewGame assigns. A field the loader writes and the reset does not is reported.

WHAT IT DOES NOT CHECK. Whether the reset's value is the RIGHT one — only that the field was
considered. That is the failure this catches: not a wrong value, an untouched one.

EXEMPT are the fields covered by whole-object replacement (`_hunter = new Hunter()` puts back
everything the loader poured into the hunter) and the pure load-path bookkeeping that a fresh game
has no analogue for. Every entry is named and justified; keep the list short, because each one is a
place this gate is not looking.
"""
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
GAME1 = os.path.join(ROOT, "src", "IdleXIdle.Game", "Game1.cs")

LOADER = "LoadOrStartFresh"
RESET = "StartNewGame"

# Fields the reset does not need to assign, and why. A whole-object replacement covers everything
# the loader poured into that object; a load-only latch has no meaning for a game with no file.
EXEMPT = {
    # Replaced wholesale by StartNewGame: `_hunter = new Hunter()` and friends.
    "_hunter", "_world", "_loadout", "_mastery", "_dust", "_skillProgress", "_characters",
    "_warren", "_traitLedger", "_expedition", "_region",
    # The save-lock trio: StartNewGame clears them at its top, before the fields it resets.
    "_saveLocked", "_saveLockReason", "_saveLockFailure",
    # Load-path bookkeeping. A fresh game has no file to have read, and SeedNewGame writes the
    # first-boot message itself.
    "_hasSave", "_bootMessage", "_bootColor", "_bootTimer", "_lastSeenUtc", "_saveVersionSeen",
    "_fifthSkillDropped", "_completedSetsLoaded", "_legacyKeystoneGrant",
}


def body(text: str, name: str) -> str:
    """The source of one method, by brace matching from its DECLARATION.

    Matched on `private void <name>(` rather than on the bare name: Game1 calls both of these
    methods, and mentions both in doc comments, so a plain search finds a CALL SITE and brace-matches
    the block around it. The first draft of this gate did exactly that and reported three fields.
    """
    m = re.search(rf"private\s+(?:void|bool)\s+{name}\s*\(", text)
    if m is None:
        raise ValueError(f"no declaration of {name}")
    at = m.start()
    open_brace = text.index("{", at)
    depth, i = 0, open_brace
    while i < len(text):
        if text[i] == "{":
            depth += 1
        elif text[i] == "}":
            depth -= 1
            if depth == 0:
                return text[open_brace:i]
        i += 1
    raise ValueError(f"unbalanced braces in {name}")


def assigned(src: str) -> set:
    """Every private field this source assigns to — `_x = `, `_x += `, `_x.Clear()` included."""
    out = set()
    for m in re.finditer(r"\b(_[A-Za-z][A-Za-z0-9]*)\s*(?:=[^=]|\+=|-=|\?\?=)", src):
        out.add(m.group(1))
    for m in re.finditer(r"\b(_[A-Za-z][A-Za-z0-9]*)\.(?:Clear|RestoreTaken|Restore)\s*\(", src):
        out.add(m.group(1))
    return out


def main() -> int:
    text = open(GAME1, encoding="utf-8-sig").read()
    loaded = assigned(body(text, LOADER))
    reset = assigned(body(text, RESET))

    missing = sorted(f for f in loaded - reset if f not in EXEMPT)
    if not missing:
        print(f"   every host field {LOADER} writes is put back by {RESET} "
              f"({len(loaded)} written, {len(EXEMPT)} exempt by name).")
        return 0

    print(f"FIELDS {LOADER} WRITES AND {RESET} LEAVES ALONE — a reset would inherit them:\n")
    for f in missing:
        print(f"  {f}")
    print(f"\nAssign it in {RESET}, or add it to EXEMPT in {os.path.basename(__file__)} with the "
          f"reason it needs no reset.")
    return 1


sys.exit(main())
