#!/usr/bin/env python3
"""No bare number may be a text size. Every drawn size comes from UiTypography.

    python3 tools/check_ui_type.py

THE COMPLAINT THIS EXISTS TO KEEP CLOSED, in the player's words (2026-08-28): "there is an
inconsistency between the text sizes inside the game -- some texts are smaller, some bigger, and some
panels' and texts' placements differ."

They were right, and it was measurable. The game had drifted to fourteen distinct drawn sizes --
12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 24, 26, 30, 36, 40 -- three of which (18, 19, 20) were
doing the same job one pixel apart, while the arena's skill rail had sunk to 12 and 13 to fit a
column it had over-inset by 34 px a side. Nothing was wrong with any single call; each was a
reasonable local choice. The drift is what the reader sees, and no reviewer reading one diff can see
it. Only a gate over the whole tree can.

WHAT IT CHECKS. Every call to the sized text helpers --

    TextBig / TextRightBig / TextCenterBig   (arg 6, the logical pixel height)
    MeasureBig                               (arg 2)
    WrapBig                                  (arg 3)

-- must be given a NAME, not a numeral. A UiTypography rung, a local constant, a variable, an
expression: anything a human had to name. A literal `14` is what this refuses.

It also refuses a bare number in a LINE PITCH slot -- `lineH = 26`, `rowH = 22`, `pitch = 20` -- because
a row height that does not follow its rung through UiTypography.Pitch(rung) overlaps the next row the
day the ladder moves (UX V2 P0.4).

And it refuses a literal in the SIZE position of a UiTypography-shaped constant declaration inside
the UI assembly, so a screen cannot re-open the ladder under a private name:

    private const int MyTitlePx = 30;      <-- flagged
    private const int MyTitlePx = UiTypography.PrimaryValue;   <-- fine

WHY A SCRIPT AND NOT A TEST. There is no test project for the Game assembly (see
check_mouse_space.py, which exists for the same reason). This is a text gate over source, like every
other check in this directory, and it runs from tools/check_all.sh.

THE OPT-OUT. A genuinely local size -- the trait tree draws in WORLD units that the camera zoom
multiplies, so a screen-pixel rung is the wrong unit there -- can name itself out with a trailing
comment marker on the same line:

    _ui.TextBig(b, s, x, y, c, 32);   // ui-size-ok: world units, scaled by the tree camera

Every opt-out must carry a reason after the colon. The gate prints them all at the end, so the list
of exceptions is visible rather than forgotten.
"""
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
GAME = ROOT / "src" / "IdleXIdle.Game"

# method name -> zero-based index of the argument that is a text size
SIZED = {
    "TextBig": 5,
    "TextRightBig": 5,
    "TextCenterBig": 5,
    "MeasureBig": 1,
    "WrapBig": 2,
}

# A declaration whose NAME says it is a text size. These are the private ladders that grow beside
# the real one; if the name ends in Px or Size or TextPx, the value has to come from UiTypography.
DECL = re.compile(
    r"\b(?:const|static\s+readonly)\s+int\s+(\w*(?:Px|TextSize|FontSize))\s*=\s*([^;]+);")

OPT_OUT = re.compile(r"//\s*ui-size-ok\s*:\s*(.+?)\s*$")

# A bare number where a LINE PITCH goes. The pitch of a list or paragraph follows its rung through
# UiTypography.Pitch(rung); a literal here is a row height that will overlap the next row the day the
# ladder moves (UX V2 P0.4).
PITCH = re.compile(r"\b(?:lineH|LineH|rowH|RowH|pitch|linePitch|rowPitch)\s*=\s*(-?\d+)\b")

# A name is anything that is not a plain integer. `-1` and `0` are numbers too.
NUMERIC = re.compile(r"^-?\d+$")


def strip_block_comments(text: str) -> str:
    """Blank out /* */ so a commented-out call is not read as code. Line comments stay: the
    opt-out marker lives in one, and the scanner needs to see it."""
    return re.sub(r"/\*.*?\*/", lambda m: "\n" * m.group(0).count("\n"), text, flags=re.S)


def split_args(s: str):
    """Split an argument list on its TOP-LEVEL commas, respecting nesting and both kinds of literal.

    A naive split fails on the very calls that matter: an interpolated string can hold a comma
    inside a hole (`$"{string.Join(", ", xs)}"`) and a nested call can hold several, so a size
    argument would be read off the wrong position and the gate would report a colour as a size.
    """
    out, depth, cur, i, instr, verbatim = [], 0, "", 0, False, False
    while i < len(s):
        ch = s[i]
        if instr:
            if verbatim:
                if ch == '"' and i + 1 < len(s) and s[i + 1] == '"':
                    cur += s[i:i + 2]
                    i += 2
                    continue
                if ch == '"':
                    instr = verbatim = False
            else:
                if ch == "\\":
                    cur += s[i:i + 2]
                    i += 2
                    continue
                if ch == '"':
                    instr = False
                # An interpolation hole can itself contain a string, and its commas are NOT
                # top-level -- but they are inside the literal, so depth is untouched either way.
            cur += ch
            i += 1
            continue
        if ch == '"':
            instr = True
            verbatim = cur.endswith("@") or cur.endswith('$@') or cur.endswith('@$')
            cur += ch
            i += 1
            continue
        if ch == "'":                       # char literal: 'a', '\'', '·'
            j = i + 1
            while j < len(s) and s[j] != "'":
                j += 2 if s[j] == "\\" else 1
            cur += s[i:j + 1]
            i = j + 1
            continue
        if ch in "([{":
            depth += 1
        elif ch in ")]}":
            depth -= 1
        if ch == "," and depth == 0:
            out.append(cur.strip())
            cur = ""
            i += 1
            continue
        cur += ch
        i += 1
    if cur.strip():
        out.append(cur.strip())
    return out


def calls(text: str, name: str):
    """Yield (offset, argument-list-source) for every call to `name(` in `text`."""
    for m in re.finditer(r"(?<![A-Za-z0-9_])" + name + r"\s*\(", text):
        i, depth, instr, verbatim = m.end(), 1, False, False
        while i < len(text) and depth > 0:
            ch = text[i]
            if instr:
                if verbatim:
                    if ch == '"' and i + 1 < len(text) and text[i + 1] == '"':
                        i += 2
                        continue
                    if ch == '"':
                        instr = verbatim = False
                else:
                    if ch == "\\":
                        i += 2
                        continue
                    if ch == '"':
                        instr = False
                i += 1
                continue
            if ch == '"':
                instr = True
                verbatim = text[i - 1] in "@$" if i else False
                i += 1
                continue
            if ch == "'":
                j = i + 1
                while j < len(text) and text[j] != "'":
                    j += 2 if text[j] == "\\" else 1
                i = j + 1
                continue
            if ch in "([{":
                depth += 1
            elif ch in ")]}":
                depth -= 1
            i += 1
        yield m.start(), text[m.end():i - 1]


def main() -> int:
    bad, allowed = [], []

    for path in sorted(GAME.glob("*.cs")):
        raw = path.read_text(encoding="utf-8")
        text = strip_block_comments(raw)
        lines = raw.split("\n")

        def line_of(offset: int) -> int:
            return text[:offset].count("\n") + 1

        def opt_out(line_no: int):
            m = OPT_OUT.search(lines[line_no - 1]) if line_no <= len(lines) else None
            return m.group(1) if m else None

        for name, idx in SIZED.items():
            for off, args_src in calls(text, name):
                args = split_args(args_src)
                if len(args) <= idx:
                    continue                      # an unsized overload, or a call we cannot read
                size = args[idx]
                if not NUMERIC.match(size):
                    continue
                ln = line_of(off)
                if (why := opt_out(ln)) is not None:
                    allowed.append((path.name, ln, f"{name}(... {size})", why))
                else:
                    bad.append((path.name, ln, f"{name}(..., {size})   a bare size"))

        # A hand-set line pitch.
        for m in PITCH.finditer(text):
            ln = line_of(m.start())
            if path.name == "UiTypography.cs":
                continue
            if (why := opt_out(ln)) is not None:
                allowed.append((path.name, ln, f"{m.group(0)}", why))
            else:
                bad.append((path.name, ln, f"{m.group(0)}   a bare line pitch (use UiTypography.Pitch(rung))"))

        # A private ladder growing beside the real one.
        for m in DECL.finditer(text):
            value = m.group(2).strip()
            if not NUMERIC.match(value):
                continue
            ln = line_of(m.start())
            if path.name == "UiTypography.cs":
                continue                          # the ladder is allowed to hold numbers
            if (why := opt_out(ln)) is not None:
                allowed.append((path.name, ln, f"{m.group(1)} = {value}", why))
            else:
                bad.append((path.name, ln,
                            f"{m.group(1)} = {value}   a size constant off the ladder"))

    if allowed:
        print(f"{len(allowed)} declared exception(s):")
        for f, ln, what, why in allowed:
            print(f"   {f}:{ln}  {what}   -- {why}")

    if not bad:
        print("every drawn text size comes from UiTypography.")
        return 0

    print("\nBARE TEXT SIZES -- these re-open the type ladder one call site at a time:\n")
    for f, ln, what in bad:
        print(f"  {f}:{ln}  {what}")
    print("\nUse a UiTypography rung (ScreenTitle / PanelTitle / Headline / NavigationLabel / Body /")
    print("Secondary / Caption ...) — or UiTypography.Pitch(rung) for a line pitch — or name the exception")
    print("on the same line with")
    print("  // ui-size-ok: <why this size is not a screen-pixel rung>")
    return 1


if __name__ == "__main__":
    sys.exit(main())
