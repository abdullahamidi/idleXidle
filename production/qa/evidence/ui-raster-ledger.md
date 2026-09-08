# UI RASTER QUALITY — the asset-scale audit (brief §74, §75, PHASE 7)

Measured 2026-09-04 from the running game on eleven screens. **Result: 0 assets over budget.**

§74's rule is *"decorative raster frames may not be arbitrarily stretched"*, and its stated cause is
*"we generated the wrong resolution and stretched it"*. That is a claim about numbers, so it is
answered with numbers rather than with an opinion about a screenshot.

## The instrument

`RH_UI_BUDGET=1` turns on `UiRasterLedger`. Every UiKit primitive that SCALES art reports the source
pixels it sampled against the destination pixels it painted, and the worst ratio each asset key
reached is printed after the shot:

```
RH_UI_BUDGET=1 RH_SHOT_MODE=<screen> RH_SHOT=<out.png> dotnet run --project src/IdleXIdle.Game
```

Seven sites report: `Sprite`, `SpriteGrounded`, `AnimSprite`, `SpriteFit`, `Background`, `Bar`, and
`NineSlice`. The bands match the VFX asset-scale budget's, so one habit reads both ledgers — `ok` at
or below native, `soft` to 1.25×, `OVER` above it.

A nine-sliced frame reports its **corner's** magnification, never the panel's overall size. A
nine-slice is the FIX for stretching rather than an instance of it: corners hold native scale, edges
tile in one direction, the centre fills. Reporting the whole panel would flag every large frame in the
game and teach nobody anything.

## What it found

| screen | assets | over |
|---|---|---|
| gear · weave · traits · map · forge · fight · world · dusttree · roster · warren · vault | 5–19 each | **0** |

The worst four draws in the whole UI, after the fix below:

```
gear    char_seeker_idle_strip8_512    src 248x398   dst 310x497   ratio 1.250  soft
warren  icon_warren_crest              src  64x64    dst  72x72    ratio 1.125  soft
fight   char_seeker_attack_strip8_512  src 452x395   dst 492x429   ratio 1.088  soft
fight   char_seeker_idle_strip8_512    src 248x398   dst 267x430   ratio 1.080  soft
```

**The frames were already right.** `ui_panel_square`, `ui_panel_vertical` and `ui_panel_medium` all
report `src 40x40 dst 40x40 ratio 1.000` on every screen that draws them — the nine-slice holds its
corners at exact native scale, which is what §75 asks for. So §74's premise about the EQUIPPED frame
being "enlarged beyond its intended scale" did not survive measurement: the frame is not magnified at
all. The polish pass that preceded this brief had already fixed it, and the ledger is what says so
rather than an assurance.

**One real violation, and it was not a frame.** The GEAR paper doll drew the hunter at
`ratio 1.327 OVER` — a 398-px figure painted 528 px tall — while the same strip in the fight drew at
1.08. The doll's box takes whatever vertical room is left in the panel, so it asked for a size the art
could not supply, and got it.

## The fix, and why it is in the primitive

`UiKit.AnimSprite` caps its scale at `UiKit.RasterCeiling` (1.25) and draws the figure at the largest
honest size, still centred and still bottom-anchored so a capped hunter keeps their feet where an
uncapped one had them. Shrinking one box to fit its art is a layout number that the next screen would
have to rediscover; capping the magnification is the RULE, and it now holds for every caller rather
than for the one that happened to be noticed.

The cap TRUNCATES rather than rounds. Rounding put the drawn height one pixel past the budget at the
ceiling exactly, and the ledger correctly reported the cap itself as a violation — 1.251 OVER.

Result: gear falls to `1.250 soft`, and no screen has an OVER row.

## Not in scope, and why

`icon_warren_crest` at 1.125 and the two champion strips at ~1.08 are `soft` — inside the same budget
the VFX contract uses, where a hand-drawn edge does not visibly soften under `LinearClamp`. They are
recorded so a future regression has a baseline, not because they are owed work.

## Retake

```
for m in gear weave traits map forge fight world dusttree roster warren vault; do
  RH_UI_BUDGET=1 RH_SHOT_MODE=$m RH_SHOT=... dotnet run --project src/IdleXIdle.Game | grep '^ui'
done
```
