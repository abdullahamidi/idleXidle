#!/usr/bin/env python3
"""The cursor is mapped ONCE, in Game1.ReadCursor; a screen never converts it.

THE BUG THIS EXISTS TO CATCH, in the player's words: "Stat upgrade tiklayamiyorum."

Every screen is authored in 1920x1080 space and drawn through a transform the host owns. Until
2026-09-01 the host handed each screen a cursor in a DIFFERENT space (480x270 canvas units) and each
screen had to invert the transform itself. Nine remembered; TrainingScreen did not, so its TRAIN
buttons were hit-tested against a cursor that could never reach them and the entire stat economy was
unreachable — while every screenshot looked fine. Two more instances shipped the same way.

The fix that landed in the UI polish pass removed the second cursor space altogether: Game1 maps the
raw mouse through Core.Presentation.PageFrame once a frame — in floating point, floored once to the
pixel — and hands every inset menu screen `PageCursor` (page space) and the fight, the expedition log
and the chest reveal `ChromeMouse` (true 1920x1080). Nothing is left for a screen to convert, so the
rule became simpler and stricter:

    1. Game1 hands a *Screen / *Panel entry point ONLY PageCursor or ChromeMouse, and the right one:
       ChromeMouse for the fight (_expedition) and the reveal; PageCursor for every inset menu screen.
       CanvasMouse, ToOverlay and the ×4 lift no longer exist anywhere in the Game assembly.
    2. A screen entry point that receives a Point cursor NEVER scales, offsets or re-rounds it —
       `mouse.X * 4`, `mouse.X / scale`, `(int)(mouse.X …)` are all refused. It hit-tests the parameter
       (or a plain alias of it) directly against the same rectangles it draws (LAW 5, LAW 6).

There is no test project for the Game assembly, which is why this is a script and not a unit test.
"""
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
GAME = ROOT / "src" / "IdleXIdle.Game"
GAME1 = GAME / "Game1.cs"

# Words that must not exist anywhere in the Game assembly any more — each is a second cursor space.
FORBIDDEN_EVERYWHERE = ("CanvasMouse", "ToOverlay(", "Display.ToCanvas(")

# Screen fields the host hands the CHROME cursor. Everything else gets the page cursor …
CHROME_FIELDS = {"_expedition"}
# … except these methods, which draw in the chrome batch whatever screen owns them (the chest reveal).
CHROME_METHODS = {"RevealWantsClick", "RevealInput"}

CURSOR_ARGS = ("PageCursor", "ChromeMouse", "_mouse", "PageMouseF", "ChromeMouseF")
WORD = "\\b"   # regex word boundary, spelled out so no shell can eat it
# `_mouse.LeftButton == Pressed` is a button, not a cursor: only a BARE `_mouse` is a cursor argument.
CURSOR_PATTERNS = {a: (WORD + "_mouse" + WORD + r"(?!\.)" if a == "_mouse" else WORD + re.escape(a) + WORD)
                   for a in CURSOR_ARGS}


def strip_comments(text: str) -> str:
    text = re.sub(r"/\*.*?\*/", lambda m: "\n" * m.group(0).count("\n"), text, flags=re.S)
    return "\n".join(line.split("//")[0] for line in text.split("\n"))


def field_types(game1: str):
    """Map `_forge` -> `ForgeScreen` from Game1's field declarations."""
    types = {}
    pattern = r"^\s*(?:private|internal|public|protected)[^;=\n]*?" + WORD + r"([A-Z]\w*(?:Screen|Panel))\s+(_\w+)"
    for m in re.finditer(pattern, game1, re.M):
        types[m.group(2)] = m.group(1)
    return types


def close_paren(text, open_paren):
    depth = 0
    for i in range(open_paren, len(text)):
        if text[i] == "(":
            depth += 1
        elif text[i] == ")":
            depth -= 1
            if depth == 0:
                return i
    return -1


def screen_calls(game1_code, fields):
    """Yield (line, field, method, args) for every `_field.Method(...)` on a screen field."""
    for m in re.finditer(WORD + r"(_\w+)\.([A-Z]\w*)\s*\(", game1_code):
        field = m.group(1)
        if field not in fields:
            continue
        end = close_paren(game1_code, m.end() - 1)
        if end < 0:
            continue
        args = game1_code[m.end():end]
        line = game1_code[:m.start()].count("\n") + 1
        yield line, field, m.group(2), args


def entry_points(text):
    """(name, cursor_param, body) for every method declared with a Point parameter."""
    for m in re.finditer(WORD + r"(?:public|internal|private|protected)" + WORD + r"[^;{}\n=]*?" + WORD + r"(\w+)\s*\(", text):
        name = m.group(1)
        end = close_paren(text, m.end() - 1)
        if end < 0:
            continue
        params = text[m.end():end]
        p = re.search(WORD + r"Point\s+(\w+)", params)
        if not p:
            continue
        # The body: either `{ ... }` or `=> ...;`
        rest = text[end + 1:]
        head = rest.lstrip()
        if head.startswith("=>"):
            body = head[: head.find(";") + 1]
        elif head.startswith("{"):
            depth, i = 0, 0
            start = len(rest) - len(head)
            for i in range(start, len(rest)):
                if rest[i] == "{":
                    depth += 1
                elif rest[i] == "}":
                    depth -= 1
                    if depth == 0:
                        break
            body = rest[start:i + 1]
        else:
            continue
        yield name, p.group(1), body


def main() -> int:
    problems = []

    # Rule 0: the second cursor space is gone.
    for path in sorted(GAME.glob("*.cs")):
        code = strip_comments(path.read_text(encoding="utf-8"))
        for word in FORBIDDEN_EVERYWHERE:
            for m in re.finditer(re.escape(word), code):
                line = code[:m.start()].count("\n") + 1
                problems.append((path.name, line, f"`{word}` is a second cursor space; the host maps the mouse once"))

    # Rule 1: Game1 hands each screen the right cursor.
    game1 = GAME1.read_text(encoding="utf-8")
    code = strip_comments(game1)
    fields = field_types(game1)
    handed = 0
    entry_methods = {}   # screen type -> the methods Game1 hands a cursor to
    for line, field, method, args in screen_calls(code, fields):
        passed = [a for a in CURSOR_ARGS if re.search(CURSOR_PATTERNS[a], args)]
        if not passed:
            continue
        handed += 1
        entry_methods.setdefault(fields[field], set()).add(method)
        want = "ChromeMouse" if field in CHROME_FIELDS or method in CHROME_METHODS else "PageCursor"
        wrong = [a for a in passed if a != want]
        if wrong:
            problems.append(("Game1.cs", line,
                             f"{field}.{method}(…) is handed {', '.join(wrong)} — a {fields[field]} takes {want}"))

    # Rule 2: no screen scales or re-rounds the cursor it is handed — checked on the entry points the
    # host actually calls with a cursor (a Point-taking line helper is not one of them).
    checked = 0
    for path in sorted(GAME.glob("*Screen.cs")):
        text = strip_comments(path.read_text(encoding="utf-8"))
        wanted = entry_methods.get(path.stem, set())
        for name, param, body in entry_points(text):
            if name not in wanted:
                continue
            checked += 1
            scaled = re.search(WORD + re.escape(param) + r"\.[XY]\s*[*/]|\(int\)\s*\(\s*" + re.escape(param) + WORD, body)
            if scaled:
                line = text[:text.find(body) + scaled.start()].count("\n") + 1
                problems.append((path.name, line, f"{name} scales its cursor `{param}` — the host already mapped it"))

    if problems:
        print("THE CURSOR IS MAPPED ONCE, IN Game1.ReadCursor — these break that:\n")
        for f, ln, what in problems:
            print(f"  {f}:{ln}  {what}")
        print("\nHand a screen PageCursor (inset menu) or ChromeMouse (fight, log, reveal) and hit-test it as is.")
        return 1

    print(f"the host hands {handed} screen entry points the mapped cursor; {checked} screen entry points hit-test it as is.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
