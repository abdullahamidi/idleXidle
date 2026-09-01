#!/usr/bin/env python3
"""Initialize() must not touch anything LoadContent() builds.

    python3 tools/check_init_order.py

WHY THIS EXISTS, precisely. MonoGame runs Initialize() and then LoadContent(). Every screen in
this game is constructed in LoadContent, so a field like `_forge` is null for the whole of
Initialize — and LoadOrStartFresh(), which restores the save, runs from Initialize. Reaching
through a screen there is a NullReferenceException that takes the game down before the window
opens.

That is not hypothetical. `_forge.RestoreChestsOpened(save.ChestsOpened)` shipped, and the game
crashed on startup for anyone with a save file. It survived because of two things that made it
invisible:

  * A FIRST launch has no save, so LoadOrStartFresh returns early. The crash starts on the
    SECOND launch — after the player has already been told the game works.
  * The screenshot rig sets RH_SHOT, and LoadOrStartFresh returns at the top when it is set, so
    the save-load path is the one path no capture can reach. Every gate and every screenshot in
    this repo was green while the game would not start.

There was already a comment in that exact method naming this failure, naming _expedition as the
example, and instructing that anything restored there be parked in a `_pending*` field. The very
next screen touched ignored it. A warning in a comment is read by whoever is already looking at
the right line; this is read by everyone.

THE RULE: inside Initialize() and anything it calls, a field assigned in LoadContent() may only
be assigned, never dereferenced. Park the value in a `_pending*` field and apply it after
LoadContent has run.
"""
import glob
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
GAME = os.path.join(ROOT, "src", "IdleXIdle.Game")


def strip_comments(text: str) -> str:
    text = re.sub(r"/\*.*?\*/", "", text, flags=re.S)
    return re.sub(r"^[ \t]*//.*$", "", text, flags=re.M)


def declaration(name: str) -> re.Pattern:
    r"""The DEFINITION of a method, not a call to it.

    The first version of this matched `\bLoadOrStartFresh\s*\(\s*\)`, which finds the CALL inside
    Initialize long before it finds the declaration — so the checker read the wrong braces, found
    nothing, and passed. It was a gate that did not gate, and it proved it by failing to flag the
    exact line that had just crashed the game. Requiring an access modifier and a return type in
    front of the name is what separates a definition from a call.
    """
    return re.compile(
        rf"\b(?:private|public|protected|internal)\b[^;{{}}()]*\b{re.escape(name)}\s*\(\s*\)\s*\{{")


def body_of(text: str, signature: re.Pattern):
    """The brace-balanced body of the first method whose signature matches, with its start line."""
    m = signature.search(text)
    if not m:
        return None, 0

    i = text.index("{", m.end() - 1)
    depth, j = 0, i
    while j < len(text):
        if text[j] == "{":
            depth += 1
        elif text[j] == "}":
            depth -= 1
            if depth == 0:
                break
        j += 1
    return text[i:j], text.count("\n", 0, i) + 1


def late_fields(text: str) -> set:
    """Fields CONSTRUCTED in LoadContent — null for the whole of Initialize."""
    body, _ = body_of(text, declaration("LoadContent"))
    return set(re.findall(r"(_[A-Za-z0-9_]+)\s*=\s*new\b", body or ""))


def called_from_initialize(text: str) -> set:
    """Initialize plus the private methods it calls — the whole pre-LoadContent reach."""
    body, _ = body_of(text, declaration("Initialize"))
    if body is None:
        return set()

    names = set(re.findall(r"\b([A-Z][A-Za-z0-9_]*)\s*\(", body))
    # One level deep is enough for this codebase and keeps the check honest rather than clever:
    # Initialize calls a handful of private setup methods and they do not nest further.
    return {"Initialize"} | names


def main() -> int:
    problems = []

    for path in sorted(glob.glob(os.path.join(GAME, "*.cs"))):
        raw = open(path, encoding="utf-8").read()
        if "void Initialize(" not in raw or "void LoadContent(" not in raw:
            continue

        text = strip_comments(raw)
        late = late_fields(text)
        if not late:
            continue

        for name in called_from_initialize(text):
            body, start = body_of(text, declaration(name))
            if body is None:
                continue

            for line_no, line in enumerate(body.split("\n"), start):
                for field in late:
                    # A dereference: `_forge.Something`. An ASSIGNMENT (`_forge = new ...`) is fine,
                    # and so is passing the field along, so only the dot form is a finding.
                    if re.search(rf"{re.escape(field)}\s*\.", line):
                        problems.append(
                            (os.path.basename(path), line_no, name, field, line.strip()[:70]))

    if not problems:
        print("nothing built in LoadContent is dereferenced during Initialize.")
        return 0

    print("INITIALIZE TOUCHES A SCREEN THAT DOES NOT EXIST YET —")
    print("these throw NullReferenceException before the window opens:\n")
    for file, line, method, field, src in problems:
        print(f"  {file}:{line}  in {method}()  ->  {field}")
        print(f"      {src}")
    print("\nPark the value in a _pending* field and apply it after LoadContent has run.")
    return 1


if __name__ == "__main__":
    sys.exit(main())
