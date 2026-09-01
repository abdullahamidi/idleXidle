# UI Fonts

Both faces art bible §7.1 asks for, bundled and redistributable. `SmoothFont` loads them by
**file name**, so renaming a file silently drops that weight — see the stems it looks for.

| File | Role | Licence |
|---|---|---|
| `IBMPlexSansCondensed-Regular.ttf` | Data face — captions, labels, prose | SIL OFL 1.1 |
| `IBMPlexSansCondensed-SemiBold.ttf` | Data face at structural weight — headings, buttons | SIL OFL 1.1 |
| `IBMPlexSansCondensed-Bold.ttf` | Data face at display weight — big values, damage | SIL OFL 1.1 |
| `Cinzel.ttf` | Ceremony face — screen titles and the fight banners only | SIL OFL 1.1 |

**Why these two.** §7.1 requires tabular (fixed-width) numerals wherever numbers appear in a
column, and digits that stay unambiguous small (0/O, 1/l/I, 5/S, 8/B). IBM Plex Sans Condensed
gives both — all ten digits are 540 units wide in every weight, so a right-aligned column keeps
its separators on one vertical line and a number does not shift when it changes weight.
`tools/check_font_digits.py` enforces that, so a future swap cannot quietly undo it. Being
condensed it also buys ~12% horizontal room over a normal-width grotesque, which is a direct fix
for the truncation the layout has been fighting. Cinzel is inscriptional Roman capitals — carved
letterforms, which is what §7.1 asks the ceremony face for; it is used only at title sizes, where
its narrow character set and small-size weakness cost nothing, and the data face is loaded behind
it as a glyph fallback so a mark it lacks does not draw as nothing.

**Adding or replacing a face:** drop the file here, add the copy rule in
`src/IdleXIdle.Game/IdleXIdle.Game.csproj` if the pattern does not already catch it,
and run `bash tools/check_all.sh`. Keep the `OFL-*.txt` beside the fonts — the licence requires
the copyright notice ship with them.
