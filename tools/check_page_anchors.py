#!/usr/bin/env python3
"""A screen that has been through the UX V2 pass lays out to the PAGE, never to the canvas.

    python3 tools/check_page_anchors.py

THE BUG THIS EXISTS TO KEEP CAUGHT, measured on 2026-09-01. GEAR's inspector was declared

    private static Rectangle DetailPanel => new(1308, Top, UiKit.PageRight(24) - 1308, ...);

which is 588 px wide on a 1920-wide page and 204 px wide on a 1536-wide one. At UI SCALE 125%
the whole item sheet became a column of ellipses. Nothing failed, nothing threw, every test
stayed green: the only way to see it was to pose the scale and look. One fixed edge against
one page-relative edge is the shape of the mistake, and it is easy to write by accident while
converting a screen one rectangle at a time.

MASTERY and TRAITS had the quieter half of the same fault: their titles were centred on 960 —
the CANVAS's middle — so at 125 % they drifted right and ran under the currency pills.

WHAT IT CHECKS. In the screens listed in CONVERTED below (the ones whose pass is done, so the
rule is a promise they have already kept), a layout literal may not be a canvas dimension:

    1920 / 1080   the canvas's width and height
    960  / 540    its centre
    1900 1880 1868 1896 …  right edges written as "1920 minus a margin"

in any of these positions:

    * a Rectangle constructed for a member whose name looks like layout
      (Panel / Rect / Box / View / Btn / Strip / Slot / Cell / Row / Bar / Plate / Card / Tab)
    * an x argument to TextCenterBig / TextCenter          (a centred title)
    * an argument to UiKit.PageRight / PageBottom          (a margin written as an absolute)

USE INSTEAD: UiKit.Page, UiKit.PageRight(inset), UiKit.PageBottom(inset), UiKit.PageCenterX,
or the panel's own UiKit.ContentLeft / ContentRight. Derive BOTH edges of a column from the
page, or fix its width and anchor exactly one edge.

WHY A WHITELIST RATHER THAN THE WHOLE TREE. The screens that have not had their pass yet are
still authored against the canvas, on purpose — converting them is what their checkpoint IS.
A gate that failed on them would be a gate everyone learns to ignore. A screen joins the list
on the commit that converts it, and the list only grows.

THE OPT-OUT, for a number that is genuinely the CANVAS's and not the page's — a full-screen
scrim, a flash, the arena's own geometry:

    _ui.Fill(b, new Rectangle(0, 0, 1920, 1080), flash);   // ui-page-ok: the canvas, not the page

Every opt-out is printed at the end, so the exceptions are visible rather than forgotten.
"""
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
GAME = ROOT / "src" / "IdleXIdle.Game"

# Screens whose UX V2 pass is done. Add a file here in the same commit that converts it.
CONVERTED = [
    "LoadoutScreen.cs",   # BUILD    — P1.4
    "MasteryScreen.cs",   # MASTERY  — P1.5
    "TraitsScreen.cs",    # TRAITS   — P1.6
    "GearScreen.cs",      # GEAR     — P1.7
    "VaultScreen.cs",     # VAULT    — P1.8
    "ForgeScreen.cs",     # FORGE    — P1.9
]

# The canvas's own numbers. 540 is deliberately absent: it collides with too many honest sizes.
CANVAS = {1920, 1080, 960}
# "1920 minus a margin" written out — the other way the canvas's right edge reaches a layout.
NEAR_RIGHT = range(1840, 1921)
NEAR_BOTTOM = range(1000, 1081)

OPT_OUT = re.compile(r"//\s*ui-page-ok\s*:\s*(.+?)\s*$")

# A member whose name says it is layout.
LAYOUT_NAME = re.compile(
    r"\b(?:Rectangle|var)\s+(\w*(?:Panel|Rect|Box|View|Btn|Button|Strip|Slot|Cell|Row|Bar|Plate|Card|Tab|Area|Column))\b"
    r"|\b(\w*(?:Panel|Rect|Box|View|Btn|Button|Strip|Slot|Cell|Row|Bar|Plate|Card|Tab|Area|Column))\s*(?:=>|=)\s*new\b")

CENTRED = re.compile(r"\.(TextCenterBig|TextCenter)\s*\(")
PAGE_CALL = re.compile(r"UiKit\.(PageRight|PageBottom)\s*\(\s*(-?\d+)\s*\)")
NUMBER = re.compile(r"(?<![\w.])(\d{3,4})(?![\w.])")


def offending(numbers):
    """The canvas numbers in this list, if any."""
    bad = []
    for n in numbers:
        v = int(n)
        if v in CANVAS or v in NEAR_RIGHT or v in NEAR_BOTTOM:
            bad.append(v)
    return bad


def main() -> int:
    problems, allowed = [], []

    for name in CONVERTED:
        path = GAME / name
        if not path.exists():
            problems.append((name, 0, "listed as converted but the file does not exist"))
            continue
        lines = path.read_text(encoding="utf-8").split("\n")

        for i, line in enumerate(lines, start=1):
            code = line.split("//")[0]
            if not code.strip():
                continue

            hits = []

            # A layout member built from a canvas number.
            if LAYOUT_NAME.search(code) and "new" in code:
                bad = offending(NUMBER.findall(code))
                if bad:
                    hits.append(f"layout rectangle built from {bad} — use UiKit.Page / PageRight / PageBottom")

            # A centred draw anchored to the canvas's middle rather than the page's.
            for m in CENTRED.finditer(code):
                tail = code[m.end():]
                # The x is the argument after the batch and the string; find the first bare number.
                bad = offending(NUMBER.findall(tail)[:2])
                if 960 in bad:
                    hits.append("centred on 960 (the canvas's middle) — use UiKit.PageCenterX")

            # PageRight(1880) and friends: an absolute dressed as an inset.
            for m in PAGE_CALL.finditer(code):
                if abs(int(m.group(2))) > 400:
                    hits.append(f"UiKit.{m.group(1)}({m.group(2)}) takes an INSET, not an absolute edge")

            if not hits:
                continue
            why = OPT_OUT.search(line)
            for h in hits:
                (allowed if why else problems).append(
                    (name, i, f"{h}   -- {why.group(1)}" if why else h))

    if allowed:
        print(f"{len(allowed)} declared exception(s):")
        for f, ln, what in allowed:
            print(f"   {f}:{ln}  {what}")

    if not problems:
        print(f"the {len(CONVERTED)} converted screens lay out to the page, not to the canvas.")
        return 0

    print("\nCANVAS NUMBERS IN A PAGE LAYOUT -- these break at UI SCALE 125% and 150%:\n")
    for f, ln, what in problems:
        print(f"  {f}:{ln}  {what}")
    print("\nDerive BOTH edges of a column from the page, or fix its width and anchor one edge.")
    print("If the number really is the canvas's (a full-screen scrim, the arena), say so on the line:")
    print("  // ui-page-ok: <why this is the canvas and not the page>")
    return 1


if __name__ == "__main__":
    sys.exit(main())
