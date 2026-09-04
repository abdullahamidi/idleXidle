#!/usr/bin/env python3
"""Every build the game composes is composed against the catalogues the WORLD taught.

Keystone knowledge and Vow knowledge used to come from the trait tree, and the tree was the only
producer either one had. They now come from region conquest, region mastery, the corruption ladder,
and from keeping a Vow's rule once without it. `PlayerLoadout.ToBuild` and `BuildComposer.Compose`
therefore take the two catalogues as arguments -- and both arguments are OPTIONAL, defaulting to the
trait tree, because the tree is still standing while this phase lands.

That default is the hazard. A screen that omits the two arguments still compiles, still runs, and
silently composes a DIFFERENT hunter's build: no world keystone, no proven Vow. The failure is
invisible because it is a wrong number, not a crash -- the gear screen would rank weapons for a build
the player does not have, and the training screen would state stats no fight would ever produce. Both
did exactly that until this gate was written.

There is no test project for the Game assembly, which is why this is a script and not a unit test.
When the trait tree is deleted the two parameters stop being optional and this gate can go with it.
"""
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
SRC = ROOT / "src"

# A call site is COMPLETE when it names both catalogues. They are always passed as the last two
# positional arguments, from a property or field whose name says what it is.
KEYSTONE_ARG = re.compile(r"\b(DiscoveredKeystones|_keystoneMenu|discoveredKeystones|keystones)\b")
VOW_ARG = re.compile(r"\b(KnownVows|_vowMenu|knownVows|vows)\b")

CALL = re.compile(r"\.ToBuild\(|BuildComposer\.Compose\(")


def call_text(text: str, start: int) -> str:
    """The argument list of the call that opens at `start`, balanced across newlines."""
    open_paren = text.index("(", start)
    depth = 0
    for i in range(open_paren, len(text)):
        if text[i] == "(":
            depth += 1
        elif text[i] == ")":
            depth -= 1
            if depth == 0:
                return text[open_paren + 1 : i]
    return text[open_paren:]


def main() -> int:
    problems = []
    checked = 0

    for path in sorted(SRC.rglob("*.cs")):
        if "obj" in path.parts or "bin" in path.parts:
            continue
        text = path.read_text(encoding="utf-8-sig")
        for m in CALL.finditer(text):
            # The declarations themselves are not call sites.
            line_start = text.rfind("\n", 0, m.start()) + 1
            line = text[line_start : text.find("\n", m.start())]
            if "public Build ToBuild" in line or "public static Build Compose" in line:
                continue
            args = call_text(text, m.start())
            checked += 1
            missing = []
            if not KEYSTONE_ARG.search(args):
                missing.append("the discovered keystones")
            if not VOW_ARG.search(args):
                missing.append("the known vows")
            if missing:
                lineno = text.count("\n", 0, m.start()) + 1
                rel = path.relative_to(ROOT).as_posix()
                problems.append(
                    f"{rel}:{lineno} composes a build without {' and '.join(missing)}.\n"
                    "     It falls back to the trait tree, which is no longer where either one\n"
                    "     comes from, so this build is not the one the fight runs."
                )

    if problems:
        print("build composition is reading a producer that moved:")
        for p in problems:
            print(f"   {p}")
        return 1

    print(f"   all {checked} build compositions pass the world's keystones and the account's vows.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
