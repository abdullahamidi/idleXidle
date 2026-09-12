#!/usr/bin/env python3
"""DRAW MUST NOT CONSUME INPUT.

    py tools/check_draw_purity.py            # gate: fail on any unexplained site
    py tools/check_draw_purity.py --audit     # table: every site, allowed or not

WHY THIS GATE EXISTS. MonoGame's fixed timestep makes AT LEAST ONE call to Update and exactly one
to Draw per tick, so a frame over budget runs Update twice and Draw once. Game1 latches the click
edge at the top of Update (`_clicked = _mouse.Pressed && _prevMouse.Released`) and overwrites
`_prevMouse` at the end of it — so on a catch-up tick the second Update erases the edge and the
single Draw that follows hit-tests nothing. A press held for three to six frames never re-arms: the
input is silently dropped.

That is not theoretical. The title screen shipped with exactly this bug (a dropped click on a slow
150 % frame, fixed 2026-09-12), and this gate is what stops it returning anywhere else.

WHAT IT CHECKS. Every method whose name starts with Draw, in the Game assembly, must not mention a
member that CONSUMES an input edge. Hover, cursor position and held-button reads are fine and are
deliberately not listed: the rule is about ACTION input, not about the word "mouse".

HOW A SITE IS EXCUSED. A line may carry a trailing `// draw-input-ok: <reason>` marker, or the
method may carry `// draw-input-ok-method: <reason>` on the line above its signature. Both require a
reason, and every excused site is printed by --audit so the list stays visible rather than becoming
invisible. The list of excused sites is the tracked record the input-architecture pass promised.
"""
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
GAME = ROOT / "src" / "IdleXIdle.Game"

# ── WHAT COUNTS AS CONSUMING AN EDGE ────────────────────────────────────────────────────────────
#
# Two kinds of finding, and the difference matters.
#
# NAMED EDGES are members that ARE an input edge. Mentioning one inside a Draw is a violation on its
# own: there is nothing presentation-only to do with it. A read of the cursor's POSITION or of a held
# button changes nothing and is deliberately absent from this list — the rule is about action input,
# not about the word "mouse".
NAMED_EDGES = [
    (r"\b_clicked\b", "the left-click edge"),
    (r"\b_rightClicked\b", "the right-click edge"),
    (r"\bMouseClicked\b", "the modal-gated left-click edge"),
    (r"\bMouseRightClicked\b", "the modal-gated right-click edge"),
    (r"\b_wheel\b", "the wheel delta"),
    (r"\bMouseWheel\b", "the wheel delta"),
    (r"\bPressed\s*\(\s*Keys\.", "a key edge"),
    (r"\bEdge\s*\(\s*Keys\.", "a key edge"),
]

# HIT-TESTING WIDGETS take the edge as one named argument. They are perfectly safe when that argument
# is the literal `false` — which is exactly the drawn-only form the reference screens (Mastery, Gear)
# use, and how a button paints its hover and its pressed face without being able to fire. So these are
# judged by the ARGUMENT, not by the call: (regex, zero-based index of the edge argument, what it is).
WIDGETS = [
    (r"\bUiKit\.ClickedIn\s*\(", 2, "a click hit-test"),
    (r"\bUiKit\.Button\s*\(", 5, "a hit-testing button"),
    (r"_ui\.Button\s*\(", 4, "a hit-testing button"),
    (r"_ui\.CloseButton\s*\(", 3, "a hit-testing close button"),
    (r"_ui\.Checkbox\s*\(", 3, "a hit-testing checkbox"),
]

# A Draw METHOD that is handed an edge at all. Not itself an action, but it is the thing that lets one
# come back — so it is reported, and it can be excused with a reason like any other site.
EDGE_PARAMS = [
    (r"\bbool\s+clicked\b", "a `clicked` parameter"),
    (r"\bbool\s+rightClicked\b", "a `rightClicked` parameter"),
    (r"\bbool\s+click\b", "a `click` parameter"),
    (r"\bint\s+wheel\b", "a `wheel` parameter"),
]

# MUTATORS: the second half of the invariant. An edge is not the only way a Draw can change the game —
# the migration of 2026-09-12 found a `Settle()` that applied last frame's request, a `Dirty = false`
# latch reset, and a currency spend, all sitting in a paint pass. Calling Draw three times with no
# Update between must change nothing semantic, so these are named and forbidden there too.
#
# Deliberately a SHORT, NAMED list rather than "any assignment": a paint pass legitimately caches
# layout, hover keys and measured rows every frame, and a check that flagged those would be noise and
# would be switched off. These are the verbs that spend, persist or advance the game.
MUTATORS = [
    (r"\bSave\s*\(\s*\)", "writes the save"),
    (r"\bSaveDisplay\s*\(\s*\)", "writes the display preferences"),
    (r"\bSpend(?:Gleam)?\s*\(", "spends currency"),
    (r"\bAddGleam\s*\(|\bAddMaterials\s*\(|\bAddDust\s*\(", "grants currency"),
    (r"\.Equip\s*\(|\.Unequip\s*\(", "equips or removes gear"),
    (r"\.Train\s*\(", "trains a stat"),
    (r"\.Upgrade\s*\(", "upgrades a facility"),
    (r"\bOpenOneChest\s*\(|\bOpenEveryChest\s*\(", "opens a chest"),
    (r"\bTakeNode\s*\(", "takes a mastery node"),
    (r"\bConsume[A-Z]\w*\s*\(", "drains a request the host owns"),
    (r"\bAcknowledge\s*\(\s*\)", "acknowledges a tutorial beat"),
    (r"\bSkipToEnd\s*\(\s*\)", "ends the authored opening"),
]


def args_of(code: str, at: int):
    """The top-level argument list of the call whose '(' follows position `at`."""
    i = code.index("(", at)
    depth, start, out = 0, i + 1, []
    for j in range(i, len(code)):
        c = code[j]
        if c in "([{":
            depth += 1
        elif c in ")]}":
            depth -= 1
            if depth == 0:
                out.append(code[start:j])
                return [a.strip() for a in out]
        elif c == "," and depth == 1:
            out.append(code[start:j])
            start = j + 1
    return None   # the call spans lines; treated as unknown by the caller

LINE_OK = re.compile(r"//\s*draw-input-ok:\s*(\S.*)$")
METHOD_OK = re.compile(r"//\s*draw-input-ok-method:\s*(\S.*)$")
DRAW_SIG = re.compile(r"^\s*(?:public|private|internal|protected)[^;(]*\b(Draw[A-Za-z0-9_]*)\s*\(")


def strip_strings(line: str) -> str:
    """Blank out string and char literals so copy never trips a pattern."""
    out, i, n = [], 0, len(line)
    while i < n:
        c = line[i]
        if c == '"':
            i += 1
            while i < n and line[i] != '"':
                i += 2 if line[i] == "\\" else 1
            i += 1
            out.append('""')
            continue
        if c == "'":
            i += 1
            while i < n and line[i] != "'":
                i += 2 if line[i] == "\\" else 1
            i += 1
            out.append("''")
            continue
        out.append(c)
        i += 1
    return "".join(out)


def draw_methods(text: str):
    """Yield (name, start_line, end_line, excuse) for every Draw* method, brace-matched."""
    lines = text.split("\n")
    for i, line in enumerate(lines):
        m = DRAW_SIG.match(line)
        if not m:
            continue
        # An expression-bodied or abstract member has no block to walk.
        j, depth, opened = i, 0, False
        while j < len(lines):
            code = strip_strings(lines[j])
            # Ignore a `//` comment tail so a brace inside a comment cannot unbalance the walk.
            code = code.split("//")[0]
            for ch in code:
                if ch == "{":
                    depth += 1
                    opened = True
                elif ch == "}":
                    depth -= 1
            if opened and depth == 0:
                break
            if not opened and ";" in code and j > i:
                break
            if not opened and ";" in code and j == i and "(" in code and ")" in code:
                break
            j += 1
        if not opened:
            continue
        excuse = None
        for back in (i - 1, i - 2, i - 3):
            if back >= 0 and (e := METHOD_OK.search(lines[back])):
                excuse = e.group(1).strip()
                break
        yield m.group(1), i, min(j, len(lines) - 1), excuse


def site(path, name, k, what, raw, excuse):
    if (e := LINE_OK.search(raw)) is not None:
        excuse = e.group(1).strip()
    return {
        "file": path.relative_to(ROOT).as_posix(), "method": name, "line": k + 1,
        "what": what, "code": raw.strip()[:110], "excuse": excuse,
    }


def scan():
    findings = []
    for path in sorted(GAME.glob("*.cs")):
        text = path.read_text(encoding="utf-8-sig").replace("\r\n", "\n")
        lines = text.split("\n")
        for name, start, end, method_excuse in draw_methods(text):
            # The signature's own lines: an edge PARAMETER is reported, the rest of the line is not
            # scanned for uses (the declaration is not a use).
            sig_end = start
            while sig_end <= end and "{" not in strip_strings(lines[sig_end]).split("//")[0]:
                sig_end += 1
            for k in range(start, min(sig_end, end) + 1):
                bare = strip_strings(lines[k]).split("//")[0]
                for pattern, what in EDGE_PARAMS:
                    if re.search(pattern, bare):
                        findings.append(site(path, name, k, what, lines[k], method_excuse))
                        break

            for k in range(min(sig_end, end) + 1, end + 1):
                raw = lines[k]
                bare = strip_strings(raw).split("//")[0]
                hit = None
                for pattern, what in NAMED_EDGES:
                    if re.search(pattern, bare):
                        hit = what
                        break
                if hit is None:
                    for pattern, what in MUTATORS:
                        if re.search(pattern, bare):
                            hit = what
                            break
                if hit is None:
                    for pattern, idx, what in WIDGETS:
                        m = re.search(pattern, bare)
                        if not m:
                            continue
                        args = args_of(bare, m.start())
                        # A call split across lines cannot be judged from one line; fall back to the
                        # conservative reading and report it, so nothing hides behind a line break.
                        if args is None:
                            hit = what + " (call spans lines)"
                            break
                        if len(args) > idx and args[idx] in ("false", "clicked: false"):
                            continue        # the drawn-only form: it can hover, it cannot fire
                        if any(a == "clicked: false" for a in args):
                            continue
                        hit = what
                        break
                if hit is not None:
                    findings.append(site(path, name, k, hit, raw, method_excuse))
    return findings


def main():
    audit = "--audit" in sys.argv
    findings = scan()
    unexplained = [f for f in findings if not f["excuse"]]
    excused = [f for f in findings if f["excuse"]]

    if audit:
        print(f"{len(findings)} edge-consuming mention(s) inside Draw* methods "
              f"({len(unexplained)} unexplained, {len(excused)} excused)\n")
        for f in findings:
            tag = "OK  " if f["excuse"] else "FAIL"
            print(f"{tag} {f['file']}:{f['line']} {f['method']}() — {f['what']}")
            print(f"       {f['code']}")
            if f["excuse"]:
                print(f"       reason: {f['excuse']}")
        print()

    if unexplained:
        for f in unexplained:
            print(f"   {f['file']}:{f['line']} {f['method']}() consumes {f['what']}")
            print(f"      {f['code']}")
        print(f"DRAW CONSUMES INPUT in {len(unexplained)} place(s) — move it to Update, or mark the "
              f"line `// draw-input-ok: <why it is safe>`.")
        return 1

    print(f"no Draw method consumes an input edge "
          f"({len(excused)} site(s) excused with a reason; --audit lists them).")
    return 0


if __name__ == "__main__":
    sys.exit(main())
