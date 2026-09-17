#!/usr/bin/env python3
"""Prepare body-face candidates that can actually pass this game's two font gates.

    python3 tools/font_candidates.py            # fetch, condition and report
    python3 tools/font_candidates.py --sheet    # ...and render the comparison sheet

WHY A TOOL AND NOT A DOWNLOAD. The renderer is FontStashSharp over StbTrueTypeSharp, which
does two things that decide which faces are even eligible:

  · it rasterises a VARIABLE font at its default instance, so a family shipping only a
    variable file would draw Regular, SemiBold and Bold identically — the hierarchy the whole
    type ladder rests on would silently vanish;
  · it applies NO OpenType features, so a family whose tabular figures live behind `tnum` is
    proportional in this game whatever its specimen page says, and
    tools/check_font_digits.py fails the build on exactly that.

Most of the Google library is now variable-only, and most sans faces hide their tabular
figures behind `tnum`. Taken at face value that leaves about ten eligible families, chosen by
packaging rather than by how they look. So this conditions each candidate instead:

  INSTANCE  fontTools' varLib.instancer pins wght to 400 / 600 / 700 and writes three real
            static faces.
  FREEZE    the `tnum` feature's substitutions are applied permanently by repointing the
            cmap's digit entries at the tabular glyphs, so the fixed-width figures are what
            the renderer gets without asking for a feature it cannot ask for.

Both are lossless with respect to the licence: every family here is SIL OFL, which permits
modification, and the reserved-font-name rule is why conditioned files are written under a
distinct stem.
"""
import argparse
import json
import os
import re
import struct
import sys
import urllib.request

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, "build", "fontcand")
API = "https://api.github.com/repos/google/fonts/contents/ofl/"
RAW = "https://raw.githubusercontent.com/google/fonts/main/"

# The shortlist, chosen for how they sit beside Cinzel's carved capitals in a dark-fantasy
# idle game that is mostly number columns — not for what happens to be packaged as static.
CANDIDATES = [
    # stem                      google dir          note
    ("Spectral",                "spectral",         "screen-first serif, low contrast, sturdy"),
    ("Bitter",                  "bitter",           "slab serif built for screens, printed-manual texture"),
    ("Vollkorn",                "vollkorn",         "sturdy old-style serif with real character"),
    ("Alegreya",                "alegreya",         "literary serif with calligraphic energy"),
    ("Literata",                "literata",         "e-reader serif, warm and even at small size"),
    ("Newsreader",              "newsreader",       "bookish serif, open counters"),
    ("CrimsonPro",              "crimsonpro",       "classic book serif, high contrast"),
    ("IBMPlexSerif",            "ibmplexserif",     "the current family's serif — same metrics discipline"),
    ("Faustina",                "faustina",         "humanist serif, compact"),
    ("ArchivoNarrow",           "archivonarrow",    "narrow grotesque, very economical"),
    ("AsapCondensed",           "asapcondensed",    "condensed grotesque, rounded terminals"),
    ("Lato",                    "lato",             "humanist sans, warm, a common Cinzel partner"),
    ("PTSansNarrow",            "ptsansnarrow",     "narrow humanist sans, plain and efficient"),
    ("SairaSemiCondensed",      "sairasemicondensed", "semi-condensed grotesque"),
    ("ZillaSlab",               "zillaslab",        "slab serif, geometric"),
    ("IBMPlexSansCondensed",    "ibmplexsanscondensed", "WHAT SHIPS TODAY — the control"),
]

WEIGHTS = [("Regular", 400), ("SemiBold", 600), ("Bold", 700)]


# ── reading a face's digit metrics, with no dependency on the conditioning above ───────────

def _tables(buf):
    num = struct.unpack_from(">H", buf, 4)[0]
    out = {}
    for i in range(num):
        tag, _s, off, length = struct.unpack_from(">4sIII", buf, 12 + 16 * i)
        out[tag.decode("latin-1")] = (off, length)
    return out


def _cmap4(buf, off, codes):
    n = struct.unpack_from(">H", buf, off + 2)[0]
    best = None
    for i in range(n):
        plat, enc, sub = struct.unpack_from(">HHI", buf, off + 4 + 8 * i)
        if struct.unpack_from(">H", buf, off + sub)[0] == 4 and (plat, enc) in ((3, 1), (0, 3), (0, 4), (0, 6)):
            best = off + sub
    if best is None:
        return {}
    seg2 = struct.unpack_from(">H", buf, best + 6)[0]
    seg = seg2 // 2
    ends = [struct.unpack_from(">H", buf, best + 14 + 2 * i)[0] for i in range(seg)]
    starts = [struct.unpack_from(">H", buf, best + 16 + seg2 + 2 * i)[0] for i in range(seg)]
    deltas = [struct.unpack_from(">h", buf, best + 16 + 2 * seg2 + 2 * i)[0] for i in range(seg)]
    at0 = best + 16 + 3 * seg2
    rngs = [struct.unpack_from(">H", buf, at0 + 2 * i)[0] for i in range(seg)]
    out = {}
    for c in codes:
        for i in range(seg):
            if starts[i] <= c <= ends[i]:
                if rngs[i] == 0:
                    out[c] = (c + deltas[i]) & 0xFFFF
                else:
                    g = struct.unpack_from(">H", buf, at0 + 2 * i + rngs[i] + 2 * (c - starts[i]))[0]
                    out[c] = (g + deltas[i]) & 0xFFFF if g else 0
                break
    return out


def digit_report(path):
    """(distinct advance widths, unitsPerEm) for the ten digits, read from the file itself."""
    buf = open(path, "rb").read()
    t = _tables(buf)
    gid = _cmap4(buf, t["cmap"][0], [ord(d) for d in "0123456789"])
    if len(gid) < 10:
        return None
    longm = struct.unpack_from(">H", buf, t["hhea"][0] + 34)[0]
    units = struct.unpack_from(">H", buf, t["head"][0] + 18)[0]
    w = {struct.unpack_from(">H", buf, t["hmtx"][0] + 4 * min(gid[ord(d)], longm - 1))[0] for d in "0123456789"}
    return sorted(w), units


# ── fetching and conditioning ─────────────────────────────────────────────────────────────

def _api(url):
    req = urllib.request.Request(url, headers={"User-Agent": "font-candidates"})
    with urllib.request.urlopen(req, timeout=60) as r:
        return json.loads(r.read())


def _get(url):
    req = urllib.request.Request(url, headers={"User-Agent": "font-candidates"})
    with urllib.request.urlopen(req, timeout=60) as r:
        return r.read()


def family_files(folder):
    """(statics, variable) file paths in a family's folder, statics keyed by weight name."""
    statics, variable = {}, None
    for e in _api(API + folder):
        if e["type"] == "dir" and e["name"] == "static":
            for s in _api(e["url"]):
                m = re.match(r"^[A-Za-z]+-([A-Za-z]+)\.ttf$", s["name"])
                if m and "Italic" not in s["name"]:
                    statics[m.group(1)] = s["path"]
        elif e["name"].endswith(".ttf"):
            if "[" in e["name"]:
                if "Italic" not in e["name"]:
                    variable = e["path"]
            else:
                m = re.match(r"^[A-Za-z]+-([A-Za-z]+)\.ttf$", e["name"])
                if m and "Italic" not in e["name"]:
                    statics[m.group(1)] = e["path"]
    return statics, variable


def freeze_tnum(path):
    """Apply `tnum` permanently: point the cmap's digits at their tabular substitutes.

    Returns True when something was actually changed. The renderer asks for no OpenType
    features, so this is the only way a family whose fixed-width figures live behind the
    feature can satisfy the game's tabular-digit gate.
    """
    from fontTools.ttLib import TTFont

    font = TTFont(path)
    if "GSUB" not in font:
        return False
    gsub = font["GSUB"].table
    wanted = set()
    for rec in gsub.FeatureList.FeatureRecord:
        if rec.FeatureTag == "tnum":
            wanted.update(rec.Feature.LookupListIndex)
    if not wanted:
        return False

    swap = {}
    for i in sorted(wanted):
        lookup = gsub.LookupList.Lookup[i]
        for sub in lookup.SubTable:
            # A single substitution is the shape every tnum implementation uses.
            mapping = getattr(sub, "mapping", None)
            if mapping:
                swap.update(mapping)
    if not swap:
        return False

    digits = {ord(d) for d in "0123456789"}
    changed = False
    for table in font["cmap"].tables:
        for code, name in list(table.cmap.items()):
            if code in digits and name in swap:
                table.cmap[code] = swap[name]
                changed = True
    if changed:
        font.save(path)
    return changed


def instance(src, dst, weight):
    from fontTools import ttLib
    from fontTools.varLib import instancer

    font = ttLib.TTFont(src)
    axes = {a.axisTag: (a.minValue, a.maxValue) for a in font["fvar"].axes}
    if "wght" in axes:
        lo, hi = axes["wght"]
        weight = max(lo, min(hi, weight))
    thin = instancer.instantiateVariableFont(font, {"wght": weight}, updateFontNames=False)
    thin.save(dst)


def prepare(stem, folder):
    os.makedirs(OUT, exist_ok=True)
    statics, variable = family_files(folder)
    made, how = [], ""
    for name, wght in WEIGHTS:
        dst = os.path.join(OUT, f"{stem}-{name}.ttf")
        alias = {"SemiBold": ["SemiBold", "Medium", "Bold"], "Regular": ["Regular"], "Bold": ["Bold", "SemiBold"]}
        src_path = next((statics[a] for a in alias[name] if a in statics), None)
        if src_path:
            open(dst, "wb").write(_get(RAW + src_path))
            how = how or "static"
        elif variable:
            var_local = os.path.join(OUT, f"_{stem}.var.ttf")
            if not os.path.exists(var_local):
                open(var_local, "wb").write(_get(RAW + variable))
            instance(var_local, dst, wght)
            how = "instanced"
        else:
            return None, f"no static and no variable file"
        made.append(name)
    frozen = freeze_tnum(os.path.join(OUT, f"{stem}-Regular.ttf"))
    for name, _ in WEIGHTS[1:]:
        freeze_tnum(os.path.join(OUT, f"{stem}-{name}.ttf"))
    return (how, frozen), None


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--only", default="")
    args = ap.parse_args()

    rows = []
    for stem, folder, note in CANDIDATES:
        if args.only and args.only.lower() not in stem.lower():
            continue
        try:
            got, err = prepare(stem, folder)
        except Exception as exc:                                  # noqa: BLE001 — reported, not raised
            rows.append(f"?? {stem:24s} {exc}")
            continue
        if err:
            rows.append(f"-- {stem:24s} {err}")
            continue
        how, frozen = got
        d = digit_report(os.path.join(OUT, f"{stem}-Regular.ttf"))
        if d is None:
            rows.append(f"?? {stem:24s} unreadable digits")
            continue
        widths, units = d
        tab = len(widths) == 1
        em = widths[0] / units if tab else sum(widths) / len(widths) / units
        rows.append(f"{'PASS' if tab else 'FAIL'} {stem:24s} {how:10s}"
                    f"{'tnum-frozen ' if frozen else '            '}"
                    f"digit={em:.3f}em  {note}")
    print("\n".join(rows))


main()
