#!/usr/bin/env python3
"""Every nav rail tile has an unlock gate, and the two tables line up.

The rail (`Nav`) and its gate table (`NavActivity`) are parallel arrays in Game1, matched by INDEX.
Nothing in C# enforces that: adding a tenth destination to `Nav` without a tenth entry in `NavActivity`
compiles, runs, and silently leaves the new screen ungated — reachable from the first frame, which is
the exact thing the gradual-unlock pass exists to prevent. An index that runs off the end is caught by
`NavUnlocked`'s bounds check and treated as OPEN, so the failure is quiet in both directions.

There is no test project for the Game assembly, which is why this is a script and not a unit test.
"""
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
GAME1 = ROOT / "src" / "ResonanceHunter.Game" / "Game1.cs"
UNLOCKS = ROOT / "src" / "ResonanceHunter.Core" / "Progression" / "Unlocks.cs"


def block(text: str, decl: str) -> str:
    """The brace-delimited initialiser that follows a declaration."""
    start = text.index(decl)
    open_brace = text.index("{", start)
    depth = 0
    for i in range(open_brace, len(text)):
        if text[i] == "{":
            depth += 1
        elif text[i] == "}":
            depth -= 1
            if depth == 0:
                return text[open_brace + 1 : i]
    raise SystemExit(f"unterminated initialiser after {decl!r}")


def main() -> int:
    game = GAME1.read_text(encoding="utf-8")
    unlocks = UNLOCKS.read_text(encoding="utf-8")

    problems = []

    nav_labels = re.findall(r'\("([A-Z]+)",\s*\'[A-Z]\',', block(game, "] Nav ="))
    activities = re.findall(r"Activity\.(\w+)", block(game, "] NavActivity ="))

    if len(nav_labels) != len(activities):
        problems.append(
            f"the rail has {len(nav_labels)} tiles but only {len(activities)} unlock gates.\n"
            f"     tiles: {', '.join(nav_labels)}\n"
            f"     gates: {', '.join(activities)}\n"
            "     A tile past the end of NavActivity is treated as ALWAYS OPEN."
        )

    # Every named activity must actually exist in the enum, or the switch falls through at runtime.
    declared = set(re.findall(r"^\s{4}(\w+),$", block(unlocks, "enum Activity"), re.M))
    for name in activities:
        if name not in declared:
            problems.append(f"NavActivity names Activity.{name}, which is not in the enum.")

    # And every activity should be reachable from the rail, or it is a gate guarding nothing.
    unreached = declared - set(activities)
    if unreached:
        problems.append(
            f"these activities have gates but no rail tile: {', '.join(sorted(unreached))}.\n"
            "     Nothing can ever open them, so their explanation is never shown."
        )

    if problems:
        print("NAV GATES BROKEN")
        for p in problems:
            print(f"  {p}")
        return 1

    print(f"   all {len(nav_labels)} rail tiles are gated, and every gate has a tile.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
