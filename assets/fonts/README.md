# UI Fonts

Both faces art bible §7.1 asks for, bundled and redistributable. `SmoothFont` loads them by
**file name**, so renaming a file silently drops that weight — see the stems it looks for.

| File | Role | Licence |
|---|---|---|
| `Spectral-Regular.ttf` | Data face — captions, labels, prose | SIL OFL 1.1 |
| `Spectral-SemiBold.ttf` | Data face at structural weight — headings, buttons | SIL OFL 1.1 |
| `Spectral-Bold.ttf` | Data face at display weight — big values, damage | SIL OFL 1.1 |
| `Cinzel.ttf` | Ceremony face — screen titles and the fight banners only | SIL OFL 1.1 |

**Why these two.** §7.1 requires tabular (fixed-width) numerals wherever numbers appear in a
column, and digits that stay unambiguous small (0/O, 1/l/I, 5/S, 8/B). Spectral gives both — every
digit is 500/1000 units in all three weights, so a right-aligned column keeps its separators on one
vertical line and a number does not shift when it changes weight. `tools/check_font_digits.py`
enforces that, so a future swap cannot quietly undo it. Cinzel is inscriptional Roman capitals —
carved letterforms, which is what §7.1 asks the ceremony face for; it is used only at title sizes,
where its narrow character set and small-size weakness cost nothing, and the data face is loaded
behind it as a glyph fallback so a mark it lacks does not draw as nothing.

**Spectral replaced IBM Plex Sans Condensed on 2026-09-09**, on the designer's call — the condensed
grotesque read as technical documentation rather than as this world. The trade is measured: set in
caps Spectral is ~22% wider with a 13% smaller x-height, so long labels lost room; its digits are 7%
narrower, so number columns gained it. `tools/font_candidates.py` is the tool that screened the
replacements, and it exists because most of the font library cannot be used here at all — this
renderer rasterises a variable font at its default instance (so a family must ship real static
weights) and applies no OpenType features (so tabular figures hiding behind `tnum` are proportional
in this game whatever the specimen says). Run `python3 tools/font_candidates.py` to re-screen, and
`python3 tools/font_sheet.py` to render every survivor inside a real panel before choosing.

**Adding or replacing a face:** drop the file here, add the copy rule in
`src/IdleXIdle.Game/IdleXIdle.Game.csproj` if the pattern does not already catch it,
and run `bash tools/check_all.sh`. Keep the `OFL-*.txt` beside the fonts — the licence requires
the copyright notice ship with them.
