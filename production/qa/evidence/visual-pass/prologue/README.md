# The prologue, photographed — 2026-09-16

Phase 4 of the visual identity pass: every beat of `OpeningScript.Prologue` stands on a plate of its
own. Until this pass the prologue borrowed the constellation, the region map and three arena
backgrounds, and the method that chose them said so ("bespoke prologue art is owed"). Six plates were
generated as one family through the house plate recipe (PixelLab pixen 640 × 360, upscaled × 3
nearest to 1920 × 1080; the prompts, the chosen job per beat and every rejected candidate are in
`tools/asset-pipeline/v2/spec.json` under `prologue`). They are keyed by `PrologueArt` in the host,
never by the script: the script is copy.

Re-run the stills with `bash tools/asset-pipeline/prologue_fixtures.sh [filter]`. The transition
filmstrip comes from the live opening (`bash tools/check_opening_flow.sh 100`), which films every new
beat's first half-second (`prologue_in_<beat>`) and BEGIN THE HUNT arriving (`begin_in`).

## The set

| Beat | Title | Plate | What it shows |
|---|---|---|---|
| 0 `pressures` | SIX PRESSURES | `plate_prologue_pressures` | six glows rising through a cracked plain |
| 1 `joints` | THE JOINTS | `plate_prologue_joints` | two ringed rune towers driven into a mountainside |
| 2 `tear` | THE NETWORK FAILED | `plate_prologue_tear` | one of the towers split, violet bleeding up the crack |
| 3 `hunter` | A HUNTER | `plate_prologue_hunter` | a hooded figure alone at the lip of the chasm, under the moon |
| 4 `stands` | SHE STANDS | `plate_prologue_stands` | deep underground, the tower's runes turning warm |
| 5 `hollow` | VERDANT HOLLOW | `plate_prologue_hollow` | the mossy hollow, a lantern-lit camp dug into the roots |

Each plate keeps its subject in the upper half and a quiet lower third, because the title and the lines
sit at `page.Height / 2 + 40`. None carries text. None adds canon: each illustrates a sentence the
script already says.

The plate is graded under the words: tinted 150 / 148 / 158, a 0.34 scrim, and a two-layer soft shade
behind the title block, sized from the measured title and lines (`Game1.DrawPrologue`). The existing
pan and zoom are unchanged, and Reduced Motion holds the plate still.

The first HUNTER plate (`53775985`) was rejected after capture: its rope frame read as a gallows. It
was replaced by `b754b861`, and beat 3 was re-shot.

## Stills

`beat<n>_{100,125,150}.png` and `beat<n>_100_reduced.png` for every beat: 24 captures, posed with
`RH_SHOT_OPENING=Prologue RH_SHOT_BEAT=<n>` (Reduced Motion adds `RH_SHOT_REDUCED=1`).

| Beat | 100 | 125 | 150 | Reduced | Notes |
|---|---|---|---|---|---|
| 0 pressures | PASS | PASS | PASS | PASS | the six glows sit above the title block; the lines read over the dark plain |
| 1 joints | PASS | PASS | PASS | PASS | the busiest plate: the rune rings run behind the body line at 100, and the shade keeps the line legible |
| 2 tear | PASS | PASS | PASS | PASS | the violet crack is the only saturated area, above the words |
| 3 hunter | PASS | PASS | PASS | PASS | the figure and the moon above; the title and line sit on the chasm's dark lower third |
| 4 stands | PASS | PASS | PASS | PASS | the warm runes above, the words on the dark floor |
| 5 hollow | PASS | PASS | PASS | PASS | the lantern camp above the lower third; the last beat's NEXT becomes BEGIN THE HUNT in the gate |

## The transition filmstrip

`filmstrip_transition_100.png`: 19 frames from the autoplayed fresh-save opening at 100 %
(`build/shots/opening_flow/100`, frame numbers at 60 Hz): each new beat's first half-second
(`prologue_in_<n>`, sampled about every 12 frames), the last beat as read (`prologue_5`), and BEGIN THE
HUNT arriving (`begin_in`).

| What the strip shows | Verdict |
|---|---|
| Every beat change is a cut through black: the first sampled frame (246, 368, 486, 605, 727) is the new beat's plate and words still near black, and twelve frames later the plate stands at full value with its title, line, progress dots and NEXT. No frame shows two plates at once and none shows the previous beat's words. | PASS |
| Each beat's plate is its own (joints, tear, hunter, stands, hollow), in script order; the progress dots advance one per beat. | PASS |
| The last beat's button reads BEGIN, and after it the prologue's picture is gone: frames 848-872 show the gate card (VERDANT HOLLOW, BEGIN THE HUNT) over the dimmed arena the hunt will open on, with the Hunter standing in it. | PASS |

The same run's trace (`tools/check_opening_trace.py`) passes every order rule, and the opening plays from
a fresh save in order at 100, 125 and 150 %.

Reduced Motion holds the plate still (the drift is pinned at its midpoint) and cuts between beats
instead of fading (`Game1.DrawPrologue`; `beat<n>_100_reduced.png`). The posed rig holds the opening
clock, so the pan itself is filmed only by the live run.
