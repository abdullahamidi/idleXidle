#!/usr/bin/env python3
"""Render every conditioned body-face candidate inside a real panel from this game.

    python3 tools/font_sheet.py

A specimen page tells you nothing useful here. What decides a body face for this game is how
it behaves in the three places it actually lives: a right-aligned number column, a wrapped
sentence of rules text, and a control's label — all at the ladder's real sizes, in the real
inks, on the real ground, beside a Cinzel title. So the sheet draws exactly that, once per
candidate, at 100% UI scale.
"""
import os
import sys

from PIL import Image, ImageDraw, ImageFont

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
CAND = os.path.join(ROOT, "build", "fontcand")
FONTS = os.path.join(ROOT, "assets", "fonts")
OUTDIR = os.path.join(ROOT, "build", "shots")

# The ladder, at 100 % (UiTypography's base values).
HEADLINE, BUTTON, BODY, SECONDARY, CAPTION = 26, 24, 22, 19, 16

# UiInk, verbatim.
GROUND = (0x15, 0x10, 0x0F)
RAISED = (0x22, 0x1A, 0x17)
PRIMARY = (0xE8, 0xDF, 0xC8)
SECOND = (0xA9, 0xA5, 0x9E)
ACCENT = (0xF6, 0xB2, 0x3A)
GOOD = (0x6E, 0xC8, 0x7A)
RULE = (0x4A, 0x3F, 0x36)

ORDER = [
    ("IBMPlexSansCondensed", "SHIPS TODAY — condensed grotesque"),
    ("Spectral",             "screen-first serif, low contrast"),
    ("Vollkorn",             "sturdy old-style serif"),
    ("Alegreya",             "literary serif, calligraphic"),
    ("Literata",             "e-reader serif, warm"),
    ("Newsreader",           "bookish serif, open counters"),
    ("CrimsonPro",           "classic book serif"),
    ("Faustina",             "humanist serif, compact"),
    ("Bitter",               "slab serif, printed-manual"),
    ("ZillaSlab",            "slab serif, geometric"),
    ("IBMPlexSerif",         "the current family's serif"),
    ("ArchivoNarrow",        "narrow grotesque, economical"),
    ("AsapCondensed",        "condensed grotesque, rounded"),
    ("Lato",                 "humanist sans, warm"),
]

CARD_W, CARD_H, COLS, PAD = 640, 400, 3, 24


def face(stem, weight, px):
    for root in (CAND, FONTS):
        p = os.path.join(root, f"{stem}-{weight}.ttf")
        if os.path.exists(p):
            return ImageFont.truetype(p, px)
    raise FileNotFoundError(f"{stem}-{weight}")


def wrap(draw, text, font, width):
    out, line = [], ""
    for word in text.split():
        trial = (line + " " + word).strip()
        if draw.textlength(trial, font=font) <= width:
            line = trial
        else:
            out.append(line)
            line = word
    if line:
        out.append(line)
    return out


def card(img, x, y, stem, note, index):
    d = ImageDraw.Draw(img)
    d.rectangle([x, y, x + CARD_W, y + CARD_H], fill=RAISED, outline=RULE)
    inner = x + 26
    room = CARD_W - 52

    # The candidate names itself, set in itself.
    name = face(stem, "SemiBold", HEADLINE)
    d.text((inner, y + 18), f"{index}.  {stem}", font=name, fill=PRIMARY)
    d.text((inner, y + 18 + 34), note.upper(), font=face(stem, "Regular", CAPTION), fill=SECOND)

    cy = y + 18 + 34 + 30
    d.line([inner, cy, x + CARD_W - 26, cy], fill=RULE)
    cy += 14

    # A number column — the thing this game is mostly made of.
    d.text((inner, cy), "HUNTER", font=face(stem, "SemiBold", SECONDARY), fill=ACCENT)
    cy += 26
    body_r = face(stem, "Regular", BODY)
    body_s = face(stem, "SemiBold", BODY)
    right = x + CARD_W - 26
    for label, value, tint in (("Attack Power", "1,190", PRIMARY),
                               ("Resonance", "2,004", PRIMARY),
                               ("Critical Chance", "12.0%", GOOD),
                               ("Gleam", "888", PRIMARY)):
        d.text((inner, cy), label.upper(), font=body_r, fill=SECOND)
        w = d.textlength(value, font=body_s)
        d.text((right - w, cy), value, font=body_s, fill=tint)
        cy += 29
    cy += 8

    # A sentence of rules text, wrapped the way the inspector wraps one.
    for line in wrap(d, "Every hit without a critical brings the next closer, up to +15%.",
                     body_r, room)[:2]:
        d.text((inner, cy), line, font=body_r, fill=PRIMARY)
        cy += 29
    cy += 10

    # A control, and a chip — the two shortest voices on the ladder.
    btn = face(stem, "SemiBold", BUTTON)
    bw = d.textlength("SELL FOR 180 GLEAM", font=btn)
    d.rectangle([inner, cy, inner + bw + 32, cy + 40], fill=GROUND, outline=ACCENT)
    d.text((inner + 16, cy + 8), "SELL FOR 180 GLEAM", font=btn, fill=ACCENT)
    cap = face(stem, "Regular", CAPTION)
    d.text((inner + bw + 52, cy + 12), "EPIC · TIER 8 · NATURE", font=cap, fill=SECOND)


def main():
    rows = (len(ORDER) + COLS - 1) // COLS
    W = PAD + COLS * (CARD_W + PAD)
    header = 150
    H = header + rows * (CARD_H + PAD) + PAD
    img = Image.new("RGB", (W, H), GROUND)
    d = ImageDraw.Draw(img)

    cinzel = ImageFont.truetype(os.path.join(FONTS, "Cinzel.ttf"), 44)
    d.text((PAD + 26, 34), "THE BODY FACE, BESIDE CINZEL", font=cinzel, fill=ACCENT)
    sub = ImageFont.truetype(os.path.join(FONTS, "IBMPlexSansCondensed-Regular.ttf"), 20)
    d.text((PAD + 26, 96),
           "Every card is one candidate, set in itself, at the game's real sizes and inks. "
           "Titles stay Cinzel in all of them.",
           font=sub, fill=SECOND)

    for i, (stem, note) in enumerate(ORDER):
        cx = PAD + (i % COLS) * (CARD_W + PAD)
        cy = header + (i // COLS) * (CARD_H + PAD)
        try:
            card(img, cx, cy, stem, note, i + 1)
        except FileNotFoundError as e:
            d.text((cx + 20, cy + 20), f"missing {e}", font=sub, fill=(0xD8, 0x48, 0x3A))

    os.makedirs(OUTDIR, exist_ok=True)
    out = os.path.join(OUTDIR, "font_candidates.png")
    img.save(out)
    print(out, img.size)


main()
