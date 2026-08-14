#!/usr/bin/env python3
"""Every character the game DRAWS must be one we have seen render.

    python3 tools/check_font_coverage.py

A character with no glyph renders as NOTHING. Not as a box, not as a question mark — the
text simply closes over the hole and reads as a different sentence. Three had been shipping:

    ↔   "WEIGHT ↔ SPREAD AND TEMPO ↔ ENDURE ARE OPPOSED" drew as
        "WEIGHT SPREAD AND TEMPO ENDURE ARE OPPOSED", which is a claim about four branches
        being mutually opposed rather than two pairs. Wrong, and confidently wrong.
    ▲   the inventory's UPGRADE badge. The entire "this beats what you are wearing" signal
        rode on a glyph nobody had checked.

None of them was noticed by reading the code, because the code is correct — the string
says what it should say. It can only be caught by looking at the pixels, or by this.

WHICH FONT THIS CHECKS, AND WHY IT IS NOT PixelFont's TABLE. The primary renderer is a
SYSTEM TTF — SmoothFont loads bahnschrift or segoeui off the player's machine and only falls
back to PixelFont when none loads. So coverage is not ours to guarantee: a glyph can be in
our fallback table and still vanish on the path everyone actually sees. That is exactly how
the arrow survived review — it IS in PixelFont, and it drew as nothing anyway.

So the allowed set is deliberately conservative: ASCII, plus the handful of marks observed
rendering in real captures. Anything else is flagged even if the fallback has it. If you
need a new symbol, put it on screen, LOOK at it, and then add it here.

There is no test project for the Game assembly, which is why this is a script rather than
an xUnit fact. Run it after touching UI strings.
"""
import glob
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
GAME = os.path.join(ROOT, "src", "ResonanceHunter.Game")
FONT = os.path.join(GAME, "PixelFont.cs")

# Characters that reach the screen through some other path than PixelFont, or that a string
# holds without ever drawing. Keep this list SHORT and justified — every entry is a hole.
EXEMPT = {
    # Asset keys, file paths and format specifiers are consumed by code, never rasterised.
}


# Observed rendering in captures on the system-TTF path. Grow it only after LOOKING.
#   ·  × — –   currency pills, item lines, wave counts — everywhere, all the time
#   → ←        the Forge's merge preview and the fight screen's callouts
#   ‹          the mastery tree's BACK, verified in the buildtree capture
PROVEN = set(" ·×—–→←‹")


def font_glyphs() -> set:
    """ASCII plus the marks proven to render. NOT PixelFont's table — see the module docstring."""
    return {chr(c) for c in range(32, 127)} | PROVEN


def strip_comments(text: str) -> str:
    """Remove // and /* */ so a comment's prose cannot be mistaken for a drawn string."""
    text = re.sub(r"/\*.*?\*/", "", text, flags=re.S)
    return re.sub(r"^[ \t]*//.*$", "", text, flags=re.M)


def literals(text: str):
    """Every double-quoted literal, including interpolated ones. Verbatim strings included."""
    for m in re.finditer(r'@?"((?:[^"\\]|\\.)*)"', text):
        yield m.group(1)


def main() -> int:
    have = font_glyphs()
    missing = {}

    for path in sorted(glob.glob(os.path.join(GAME, "*.cs"))):
        if os.path.basename(path) == "PixelFont.cs":
            continue
        text = strip_comments(open(path, encoding="utf-8").read())
        for line_no, line in enumerate(text.split("\n"), 1):
            for lit in literals(line):
                for ch in lit:
                    if ord(ch) < 128 or ch in have or ch in EXEMPT:
                        continue
                    missing.setdefault(ch, []).append(
                        f"{os.path.basename(path)}:{line_no}")

    if not missing:
        print(f"every drawn character is in the proven set ({len(PROVEN) - 1} marks beyond ASCII).")
        return 0

    print("UNPROVEN CHARACTERS — these may draw as nothing on the system font:\n")
    for ch, where in sorted(missing.items()):
        seen = ", ".join(dict.fromkeys(where))[:110]
        print(f"  {ch!r}  U+{ord(ch):04X}   {seen}")
    print("\nUse a character from the proven set, or prove this one with a capture and add it.")
    return 1


if __name__ == "__main__":
    sys.exit(main())
