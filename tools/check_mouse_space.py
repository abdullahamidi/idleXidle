#!/usr/bin/env python3
"""Every screen entry point handed the canvas cursor must convert it before hit-testing.

THE BUG THIS EXISTS TO CATCH, in the player's words: "Stat upgrade tiklayamiyorum."

Screens are AUTHORED in 1920x1080 and DRAWN through an inset transform, but Game1 hands them
`CanvasMouse`, which lives in 480x270 canvas space. Each screen has to invert the transform itself
with `Game1.ToOverlay` (or, for the fight screen, its own documented `mouse.X * 4`). Nine screens
remembered. TrainingScreen did not -- so its nearest TRAIN button, authored at x=938, was hit-tested
against a cursor whose x can never exceed ~479. `Rectangle.Contains` was false at every cursor
position on every frame, and the entire stat economy was unreachable.

Nothing in C# enforces this: the types match, it compiles, it draws correctly, and only the CLICKS
are dead. That is the worst possible failure shape -- it looks fine in a screenshot. A second
instance (HuntScreen.DrawLog's page buttons) shipped the same way and was found the same
day.

There is no test project for the Game assembly, which is why this is a script and not a unit test.
"""
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
GAME = ROOT / "src" / "IdleXIdle.Game"
GAME1 = GAME / "Game1.cs"

# A method converts if it does either of the two sanctioned things.
CONVERSIONS = ("Game1.ToOverlay", "ToOverlay(", "mouse.X * 4", "mouse.X*4")

# Signs a method actually hit-tests something.
#
# `ClickedIn` and `Hover` belong here, and their absence was a hole the RAW_HIT pattern below already
# knew about -- it lists both, so the gate's two halves disagreed about what a hit test even is. A
# screen that reaches the cursor ONLY through `UiKit.ClickedIn(rect, mouse, clicked)` was reported as
# "draws only; nothing to mis-hit" and skipped whole. RosterScreen.Update is exactly that shape and
# was being waved through; it happens to convert correctly, so nothing was broken -- but the one
# screen the gate could not see is the one where the next dead click would have hidden.
HIT_TESTS = (".Contains(", "_ui.Button(", "ui.Button(", "ClickedIn(", "Hover(")

# A hit test applied DIRECTLY to the raw cursor parameter, in a method that also converts.
#
# THE HOLE THIS CLOSES, and it had a live instance. The check below is "does the reachable body
# convert anywhere", which passes a method that converts once and then hit-tests the RAW parameter
# somewhere else. ForgeScreen.Update did exactly that: it converted for the loot-card loop and tested
# `BagPanel.Contains(mouse)` for the bag's scroll wheel, against a 1920-space rect. The gate reported
# all 21 entry points green while the Forge's item list could not be scrolled at all.
#
# One conversion in a method is not a property of the method; it is a property of one call site.
RAW_HIT = r"(?:\.Contains|ClickedIn|Hover)\s*\([^;()]*\b%s\b[^;()]*\)|Button\s*\([^;()]*,\s*%s\s*,"


def cursor_param(text, name):
    """
    The name of the Point parameter a screen entry point receives, or None.

    Searched across every overload: the first `Draw` is often the pose-free `Draw(SpriteBatch b)`
    forwarder, which takes no cursor at all, and stopping there would skip the raw-hit check on the
    overload that actually receives one.
    """
    for m in re.finditer(decl_pattern(name, False), text):
        args = body_args(text, m.end() - 1)
        p = re.search(r"\bPoint\s+(\w+)", args or "")
        if p:
            return p.group(1)
    return None


def body_args(text, open_paren):
    """The text between a declaration's parentheses."""
    end = close_paren(text, open_paren)
    return text[open_paren + 1:end] if end >= 0 else None

# Built by concatenation, not str.format: the character class contains literal braces, which
# format() reads as replacement fields and raises on.
#
# NON-GREEDY, AND `=` IS EXCLUDED, because the greedy spelling found a CALL instead of a declaration.
# `public void Draw(SpriteBatch b) => Draw(b, new Point(-1, -1), false);` is one line carrying two
# `Draw(`, and a greedy head walks to the RIGHTMOST -- the delegating call inside the expression
# body. The gate then measured from the middle of an expression and announced four screens as
# "Game1 calls it but it is not public there" while all four were public and correct.
#
# A declaration head never contains `=` (a default value sits inside the parentheses, after the
# name), so excluding it stops the scan at `=>` and keeps the match on the declaration's side.
DECL_HEAD = r"\b(?:public|internal|private|protected)\b[^;{}\n=]*?\b"
PUBLIC_HEAD = r"\b(?:public|internal)\b[^;{}\n=]*?\b"
DECL_TAIL = r"\s*\("


def decl_pattern(name, any_visibility):
    head = DECL_HEAD if any_visibility else PUBLIC_HEAD
    return head + re.escape(name) + DECL_TAIL


def field_types(game1):
    """Map `_forge` -> `ForgeScreen` from Game1's field declarations."""
    types = {}
    pattern = r"^\s*(?:private|internal|public|protected)[^;=\n]*?\b([A-Z]\w*(?:Screen|Panel))\s+(_\w+)"
    for m in re.finditer(pattern, game1, re.M):
        types[m.group(2)] = m.group(1)
    return types


def close_paren(text, open_paren):
    """The index of the `)` matching the `(` at `open_paren`, or -1."""
    depth = 0
    for i in range(open_paren, len(text)):
        if text[i] == "(":
            depth += 1
        elif text[i] == ")":
            depth -= 1
            if depth == 0:
                return i
    return -1


def body_after(text, start):
    """
    A method's body, whether it is written with braces or with `=>`.

    EXPRESSION BODIES ARE NOT OPTIONAL TO HANDLE, and this gate spent a while proving it. The first
    version looked for the next `{` after the declaration and matched braces from there. An
    expression-bodied member has no such brace, so the scan sailed past its `;` and returned THE NEXT
    METHOD'S BODY -- one method's code filed under another method's name.

    That produced a false alarm nobody could act on: `MapScreen.Update` is seven lines of keyboard
    handling that calls `Def`, and `private static RegionDefinition Def(int i) => Regions.All[i];`
    resolved to the body of `DrawCheckpoints`, which hit-tests. The gate reported that MAP was
    entirely unclickable while MAP was fine, and this file's own comment says what that costs: "a gate
    that cries wolf on working code gets switched off, which costs more than the hole it was closing."

    The same bug hides real ones just as easily. A screen whose helper is expression-bodied borrowed
    the NEXT method's text, so a conversion that helper never performed could be read as present.

    `start` is just inside the declaration's `(`, so the parameter list is skipped first -- a default
    value or a lambda in an attribute must not be mistaken for the body.
    """
    end = close_paren(text, start - 1)
    if end < 0:
        return None

    brace = text.find("{", end)
    arrow = text.find("=>", end)

    # An interface/abstract declaration ends at `;` with neither, and has no body to return.
    semi = text.find(";", end)
    if brace < 0 and arrow < 0:
        return None
    if arrow >= 0 and (brace < 0 or arrow < brace):
        # `=> expression;` -- run to the semicolon that closes it, ignoring any nested in braces,
        # brackets, parens or a lambda's own body (`=> new[] { 1, 2 };` is one expression).
        depth = 0
        for i in range(arrow, len(text)):
            c = text[i]
            if c in "{([":
                depth += 1
            elif c in "})]":
                depth -= 1
            elif c == ";" and depth == 0:
                return text[arrow:i + 1]
        return None
    if 0 <= semi < brace:
        return None

    depth = 0
    for i in range(brace, len(text)):
        if text[i] == "{":
            depth += 1
        elif text[i] == "}":
            depth -= 1
            if depth == 0:
                return text[brace:i + 1]
    return None


def method_bodies(text, name, any_visibility=False):
    """
    The body of EVERY overload with this name.

    ALL OF THEM, not the first, because the delegating overload is the one declared first and it is
    never the one that hit-tests: four screens spell the pose-free entry as
    `public void Draw(SpriteBatch b) => Draw(b, new Point(-1, -1), false);` above the real
    three-argument Draw. Reading only the first hands the gate a one-line forwarder and calls the
    screen clean. Following the forwarder's own call does not rescue it either -- the name is already
    marked seen by then. A union can only widen what the gate inspects, which is the safe direction
    for a lint to be wrong in.
    """
    bodies = []
    for m in re.finditer(decl_pattern(name, any_visibility), text):
        body = body_after(text, m.end())
        if body is not None:
            bodies.append(body)
    return bodies


def method_body(text, name, any_visibility=False):
    """Every overload's body as one blob, or None when the method is not declared here."""
    bodies = method_bodies(text, name, any_visibility)
    return "\n".join(bodies) if bodies else None


def reachable_body(text, entry):
    """
    The entry method's body PLUS every same-file method it transitively calls.

    THE GATE IS USELESS WITHOUT THIS, and the first draft proved it. TrainingScreen.Draw contains no
    hit-test of its own -- it calls DrawPrimary, which calls DrawTrain, which is where the dead
    `_ui.Button` lives. A body-only check reported TrainingScreen clean while the reported bug sat two
    calls below it. That is the same shape as an earlier gate here that matched a CALL instead of a
    DECLARATION and passed with the real bug present, so it gets the same treatment: follow the calls.
    """
    seen = set()
    collected = []
    queue = [entry]

    while queue:
        name = queue.pop()
        if name in seen:
            continue
        seen.add(name)

        body = method_body(text, name, any_visibility=True)
        if body is None:
            continue
        collected.append(body)

        for called in set(re.findall(r"\b([A-Z]\w*)\s*\(", body)):
            if called in seen:
                continue
            if re.search(decl_pattern(called, True), text):
                queue.append(called)

    return "\n".join(collected)


def main():
    game1 = GAME1.read_text(encoding="utf-8")
    types = field_types(game1)

    # Every `_field.Method(... CanvasMouse ...)` call in Game1 -- the screen entry points.
    calls = set()
    for m in re.finditer(r"(_\w+)\.(\w+)\(([^;]*?)\)\s*;", game1, re.S):
        field, method, args = m.group(1), m.group(2), m.group(3)
        if "CanvasMouse" in args and field in types:
            calls.add((types[field], method))

    if not calls:
        print("  NO SCREEN ENTRY POINTS FOUND -- this gate has stopped looking at anything.")
        return 1

    problems = []
    checked = 0

    for type_name, method in sorted(calls):
        path = GAME / (type_name + ".cs")
        if not path.exists():
            problems.append("%s.%s -- no file %s" % (type_name, method, path.name))
            continue

        text = path.read_text(encoding="utf-8")
        if method_body(text, method) is None:
            problems.append("%s.%s -- Game1 calls it but it is not public there" % (type_name, method))
            continue

        # Follow the call graph: the hit-test is usually several helpers below the entry point.
        body = reachable_body(text, method)
        checked += 1

        if not any(h in body for h in HIT_TESTS):
            continue                                   # draws only; nothing to mis-hit

        if any(c in body for c in CONVERSIONS):
            # It converts -- but a conversion is a property of one call site, not of the method.
            # Any hit test still aimed at the RAW parameter is a dead click hiding behind a live one.
            # THE ENTRY METHOD'S OWN BODY, NOT THE REACHABLE CLOSURE. Inside the entry point the
            # parameter name unambiguously means the raw cursor; three helpers down it does not.
            # TrainingScreen.DrawTrain, VaultScreen and MasteryScreen all take a parameter ALSO called
            # `mouse` and are called with the already-converted local, so searching the closure
            # flagged three correct screens on a name collision. A gate that cries wolf on working
            # code gets switched off, which costs more than the hole it was closing.
            raw = cursor_param(text, method)
            own = method_body(text, method) or ""
            if raw:
                stray = re.search(RAW_HIT % (re.escape(raw), re.escape(raw)), own)
                if stray:
                    problems.append(
                        "%s.%s converts the cursor, then hit-tests the RAW one anyway:\n"
                        "       %s\n"
                        "     Its rects are authored in 1920x1080; `%s` is the 480x270 canvas cursor.\n"
                        "     Fix: aim this test at the converted local too."
                        % (type_name, method, stray.group(0).strip()[:96], raw)
                    )
            continue

        problems.append(
            "%s.%s hit-tests but never converts the cursor.\n"
            "     Game1 passes CanvasMouse (480x270); its rects are authored in 1920x1080.\n"
            "     Every Contains/Button reachable from it is false at EVERY cursor position.\n"
            "     Fix: `var hit = Game1.ToOverlay(mouse);` first, then hit-test against `hit`."
            % (type_name, method)
        )

    if problems:
        print("MOUSE SPACE BROKEN")
        for p in problems:
            print("  " + p)
        return 1

    print("   all %d screen entry points convert the cursor before hit-testing." % checked)
    return 0


if __name__ == "__main__":
    sys.exit(main())
