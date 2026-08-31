#!/usr/bin/env python3
"""The bundled data font must have TABULAR digits, or every number column jitters.

    python3 tools/check_font_digits.py

WHY THIS IS A GATE AND NOT A PREFERENCE. Art bible 7.1 makes it a hard requirement:
"Tabular (fixed-width) numerals are mandatory wherever numbers appear in a column, so a
player can scan a column vertically without re-parsing each row." This game is almost
entirely number columns -- nine stat rows, affix lists, material counts, the wave ledger,
the Warren's rates.

With PROPORTIONAL digits a "1" is roughly half the width of a "0", so a right-aligned
column of 1,190 / 2,004 / 888 has its thousands separator land in three different places
and the eye has to re-find the decimal on every row. Nothing looks broken -- it just reads
slowly, and the cause is invisible in the source, because the source is a correct
right-align of a correct string.

It had never been checked. The game shipped with NO bundled font at all: SmoothFont read
bahnschrift.ttf off the player's own Windows install, so the metrics of every number in
the game were whatever that machine happened to have, and on a machine without it the
whole UI silently dropped to the pixel fallback.

WHAT IT CHECKS. Every .ttf/.otf in assets/fonts is parsed directly (no dependencies) and
the ten digits' advance widths are compared. A DISPLAY face is exempt -- it is used only
for screen titles, never for a column of numbers -- so faces named in DISPLAY_FACES are
reported but not enforced.
"""
import glob
import os
import struct
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
FONTS = os.path.join(ROOT, "assets", "fonts")

# Faces used only at title sizes, never in a column. Exempt from the tabular rule -- an
# inscriptional face has no business having fixed-width digits, and asking for it would
# rule out every display font there is.
DISPLAY_FACES = {"Cinzel"}


def tables(buf: bytes) -> dict:
    """Tag -> (offset, length) from the sfnt table directory."""
    num = struct.unpack_from(">H", buf, 4)[0]
    out = {}
    for i in range(num):
        tag, _sum, off, length = struct.unpack_from(">4sIII", buf, 12 + 16 * i)
        out[tag.decode("latin-1")] = (off, length)
    return out


def cmap_lookup(buf: bytes, off: int, codes) -> dict:
    """Map codepoints to glyph ids through a format-4 or format-12 subtable."""
    n = struct.unpack_from(">H", buf, off + 2)[0]
    best = None
    for i in range(n):
        plat, enc, sub = struct.unpack_from(">HHI", buf, off + 4 + 8 * i)
        fmt = struct.unpack_from(">H", buf, off + sub)[0]
        # Prefer a full-Unicode format 12; otherwise take a BMP format 4.
        if fmt == 12 and (plat, enc) in ((3, 10), (0, 4), (0, 6)):
            best = (12, off + sub)
            break
        if fmt == 4 and best is None and (plat, enc) in ((3, 1), (0, 3), (0, 4), (0, 6)):
            best = (4, off + sub)
    if best is None:
        return {}

    fmt, sub = best
    found = {}
    if fmt == 12:
        groups = struct.unpack_from(">I", buf, sub + 12)[0]
        for g in range(groups):
            start, end, gid = struct.unpack_from(">III", buf, sub + 16 + 12 * g)
            for c in codes:
                if start <= c <= end:
                    found[c] = gid + (c - start)
        return found

    seg2 = struct.unpack_from(">H", buf, sub + 6)[0]
    seg = seg2 // 2
    ends = sub + 14
    starts = ends + seg2 + 2
    deltas = starts + seg2
    ranges = deltas + seg2
    for s in range(seg):
        end = struct.unpack_from(">H", buf, ends + 2 * s)[0]
        start = struct.unpack_from(">H", buf, starts + 2 * s)[0]
        delta = struct.unpack_from(">h", buf, deltas + 2 * s)[0]
        ro = struct.unpack_from(">H", buf, ranges + 2 * s)[0]
        for c in codes:
            if not start <= c <= end:
                continue
            if ro == 0:
                found[c] = (c + delta) & 0xFFFF
            else:
                at = ranges + 2 * s + ro + 2 * (c - start)
                gid = struct.unpack_from(">H", buf, at)[0]
                found[c] = (gid + delta) & 0xFFFF if gid else 0
    return found


def advances(path: str, codes) -> dict:
    """Codepoint -> advance width, in font units."""
    buf = open(path, "rb").read()
    t = tables(buf)
    if "cmap" not in t or "hhea" not in t or "hmtx" not in t:
        raise ValueError("missing a required table")

    gids = cmap_lookup(buf, t["cmap"][0], codes)
    long_metrics = struct.unpack_from(">H", buf, t["hhea"][0] + 34)[0]
    hmtx = t["hmtx"][0]

    out = {}
    for c, gid in gids.items():
        # Past numberOfHMetrics every glyph shares the last recorded advance.
        i = min(gid, long_metrics - 1)
        out[c] = struct.unpack_from(">H", buf, hmtx + 4 * i)[0]
    return out


def main() -> int:
    files = sorted(glob.glob(os.path.join(FONTS, "*.ttf")) + glob.glob(os.path.join(FONTS, "*.otf")))
    if not files:
        print("NO FONT BUNDLED — assets/fonts has no .ttf/.otf, so the game falls back to a")
        print("system font that may not exist on the player's machine. Bundle an open face.")
        return 1

    digits = [ord(d) for d in "0123456789"]
    bad = False
    for path in files:
        name = os.path.basename(path)
        display = any(d.lower() in name.lower() for d in DISPLAY_FACES)
        try:
            adv = advances(path, digits)
        except (ValueError, struct.error, IndexError) as e:
            print(f"  {name}: UNREADABLE — {e}")
            bad = True
            continue

        missing = [chr(c) for c in digits if c not in adv]
        if missing:
            print(f"  {name}: NO GLYPH for {''.join(missing)} — digits would draw as nothing")
            bad = True
            continue

        widths = sorted(set(adv.values()))
        if len(widths) == 1:
            print(f"  {name}: tabular ({widths[0]} units){'  [display]' if display else ''}")
        elif display:
            print(f"  {name}: proportional, {len(widths)} widths — allowed, display face only")
        else:
            worst = max(widths) - min(widths)
            print(f"  {name}: PROPORTIONAL — {len(widths)} distinct digit widths, "
                  f"spread {worst} units ({100.0 * worst / max(widths):.0f}%)")
            print(f"    A right-aligned number column set in this face will not line up.")
            print(f"    Either pick a face with tabular figures, or add it to DISPLAY_FACES")
            print(f"    and stop using it for anything in a column.")
            bad = True

    return 1 if bad else 0


if __name__ == "__main__":
    sys.exit(main())
