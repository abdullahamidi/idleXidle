# The HUNT tile's live bar, photographed — 2026-09-16

The three signals the HUNT tile carries while the player is on any OTHER screen — the thin health bar
in its foot band, the red wash of a bite, the gold wash of a cleared wave — at 100 / 125 / 150, under
Reduced Motion, and under a chest reveal, looked at rather than reasoned about. Until this set the bar
had never been photographed with anything in it: every menu capture runs the save's fight underneath
but never drains it, so the bar was full in every picture and a full bar proves only that a bar was
drawn. The two washes live a tenth and a fifth of a second on the fight's own clock, so neither had
been posed at all.

The arithmetic (the bar under the label's line box, on the tile, clear of the icon and the edges, at
every profile) is `tests/unit/IdleXIdle.Game.Tests/nav_tile_bar_test.cs`; the rectangle it holds is
`NavTileGrid.HealthBar`, which `Game1.DrawHexNav` now reads instead of typing its own four literals.
The pictures are what the test cannot say: whether the bar is visible, whether its length is the
fight's health, whether it survives the wash it sits over.

`_crops/` holds each picture's HUNT tile (canvas 0,0,180,98) at 4×, because the bar is 4 px tall on a
1920×1080 frame; `<shot>.png.events.txt` is the wave the shot was taken from, with the champion's
health at the shutter on its first line.

## Re-running the whole matrix

```
OUT=production/qa/evidence/visual-pass/hunttile
for s in 100 125 150; do
  RH_SHOT_UISCALE=$s RH_SHOT_HUNT_T=6 RH_SHOT_DUMP=1                            bash tools/asset-pipeline/capture.sh forge $OUT/bar_forge_$s.png
  RH_SHOT_UISCALE=$s RH_SHOT_HUNT_T=6 RH_SHOT_DUMP=1 RH_SHOT_HUNTTILE=hurt:0.15    bash tools/asset-pipeline/capture.sh forge $OUT/hurt_$s.png
  RH_SHOT_UISCALE=$s RH_SHOT_HUNT_T=6 RH_SHOT_DUMP=1 RH_SHOT_HUNTTILE=cleared:0.15 bash tools/asset-pipeline/capture.sh forge $OUT/cleared_$s.png
  RH_SHOT_UISCALE=$s RH_SHOT_HUNT_T=6 RH_SHOT_DUMP=1 RH_SHOT_HUNTTILE=boss:0.15    bash tools/asset-pipeline/capture.sh forge $OUT/boss_$s.png
done
RH_SHOT_UISCALE=100 RH_SHOT_HUNT_T=6 RH_SHOT_DUMP=1 RH_SHOT_REDUCED=1 RH_SHOT_HUNTTILE=hurt:0.15    bash tools/asset-pipeline/capture.sh forge $OUT/hurt_100_reduced.png
RH_SHOT_UISCALE=100 RH_SHOT_HUNT_T=6 RH_SHOT_DUMP=1 RH_SHOT_REDUCED=1 RH_SHOT_HUNTTILE=cleared:0.15 bash tools/asset-pipeline/capture.sh forge $OUT/cleared_100_reduced.png
RH_SHOT_UISCALE=100 RH_SHOT_HUNT_T=6 RH_SHOT_DUMP=1 RH_SHOT_HUNTTILE=hurt:0.15 bash tools/asset-pipeline/capture.sh lootforge $OUT/hurt_under_reveal_100.png 1.2
```

Fifteen captures, ~40 s each. The two dials are new with this set (see the header of
`tools/asset-pipeline/capture.sh`):

- `RH_SHOT_HUNT_T=<seconds>` drains the fight running UNDER a menu fixture — the fight fixture's own
  three lines (the 1400 / 9 baseline pinned through the host, `DevStart`, a pending `DevSeek`), for
  every mode that is not a fight or a boss pose. A dial of its own because `RH_SHOT_T` already means
  the reveal's instant on `lootforge`.
- `RH_SHOT_HUNTTILE=hurt|cleared|boss[:<0..1>]` re-poses one wash every frame with `UiMotion.PoseFlash`,
  through the gate the live ticks ask (`!AttentionOwnedAbove(Coach)`), and for `hurt` through the arm's
  own two as well (`_showScreenFlash && !UiMotion.Reduced`). A pose the game would refuse photographs
  no wash — which is the picture, not a failed dial.

The bar's length is checked against the dump: the fill is `UiKit.Bar`'s degenerate branch at h = 4
(2 px tall, `(int)(126 × pct)` wide from x = 27), and the champion's maximum is reconstructed from the
wave's events (health at the shutter + every `EnemyStrike` on slot 0 up to the playhead; wave 1 of a
fresh `DevStart` opens at full health). Measured with PIL on row 92.

## The matrix

| State | Fixture | Dials | Files |
|---|---|---|---|
| the bar, drained | `forge` | `RH_SHOT_HUNT_T=6 RH_SHOT_DUMP=1` | `bar_forge_{100,125,150}` |
| the bite's red wash over it | `forge` | + `RH_SHOT_HUNTTILE=hurt:0.15` | `hurt_{100,125,150}` |
| a cleared wave's gold wash | `forge` | + `RH_SHOT_HUNTTILE=cleared:0.15` | `cleared_{100,125,150}` |
| a cleared BOSS wave's brighter wash | `forge` | + `RH_SHOT_HUNTTILE=boss:0.15` | `boss_{100,125,150}` |
| Reduced Motion, a bite | `forge` | + `RH_SHOT_REDUCED=1 RH_SHOT_HUNTTILE=hurt:0.15` | `hurt_100_reduced` |
| Reduced Motion, a cleared wave | `forge` | + `RH_SHOT_REDUCED=1 RH_SHOT_HUNTTILE=cleared:0.15` | `cleared_100_reduced` |
| a bite posed UNDER a chest reveal | `lootforge` | `RH_SHOT_T=1.2` (third arg) + `RH_SHOT_HUNTTILE=hurt:0.15` | `hurt_under_reveal_100` |

## What the pictures said

The tile's wash is measured as the mean colour of a 20×16 patch of the tile's own ground (canvas 4,4)
against the same patch of the GEAR tile under it, which never washes. The fill is the contiguous run
of `UiInk.Good` pixels on row 92 from x = 27, out of the 126 the well holds.

| Picture | Dump (health / max) | Expected fill | Measured fill | Wash (Δ over GEAR) | What it shows | Verdict |
|---|---|---|---|---|---|---|
| `bar_forge_100` | 189 / 213 = 0.887 | 111 px | 111 px = 0.881, green | none (0,0,0) | The bar under HUNT, in the foot band, well and frame visible, four bites deep. Clear of the label, the icon, and the GEAR tile below. | PASS |
| `bar_forge_125` | 189 / 213 | 111 px | 111 px, green | none | The label a rung bigger, the icon bigger; the bar in the same 10 px band, still clear. | PASS |
| `bar_forge_150` | 189 / 213 | 111 px | 111 px, green | none | The 36 px label's ink ends on row 75, the bar's fill is rows 92–93: sixteen clear rows between. | PASS |
| `hurt_100` | 189 / 213 | 111 px | 111 px, green | red (+56,+17,+13) | The whole tile washed red at the bite's brightest, the green bar on top of it — distinct, not merged. | PASS |
| `hurt_125` | 189 / 213 | 111 px | 111 px, green | red (+56,+17,+13) | Same at 125. | PASS |
| `hurt_150` | 189 / 213 | 111 px | 111 px, green | red (+56,+17,+13) | Same at 150; the bigger label reads through the wash. | PASS |
| `cleared_100` | 189 / 213 | 111 px | 111 px, green | gold (+38,+28,+7) | The tile washed gold at 0.18, the bar over it. | PASS |
| `cleared_125` | 189 / 213 | 111 px | 111 px, green | gold (+38,+28,+7) | Same at 125. | PASS |
| `cleared_150` | 189 / 213 | 111 px | 111 px, green | gold (+38,+28,+7) | Same at 150. | PASS |
| `boss_100` | 189 / 213 | 111 px | 111 px, green | gold (+65,+47,+13) | The boss mark: the same gold, visibly brighter (0.30 against 0.18). | PASS |
| `boss_125` | 189 / 213 | 111 px | 111 px, green | gold (+65,+47,+13) | Same at 125. | PASS |
| `boss_150` | 189 / 213 | 111 px | 111 px, green | gold (+65,+47,+13) | Same at 150. | PASS |
| `hurt_100_reduced` | 189 / 213 | 111 px | 111 px, green | none (0,0,0) | Reduced Motion refuses the red wash — the pose keeps the arm's own gate — and the bar stays: it is state, not motion. | PASS |
| `cleared_100_reduced` | 189 / 213 | 111 px | 111 px, green | gold (+38,+28,+7) | The gold wash still plays under Reduced Motion. That is the live arm's rule, not the pose's: only the bite checks `UiMotion.Reduced`, and UiMotion keeps pulses under Reduced as short fades. | PASS (noted) |
| `hurt_under_reveal_100` | 152 / 180 = 0.844 | 106 px | 106 px, green (dimmed) | none: tile (2,2,2) = GEAR (2,2,2) | The chest reveal owns the frame: NO red wash (the Coach gate refused the pose), and the bar is still drawn at its true length, dimmed with the whole page under the reveal's scrim. | PASS |

Every fill matches the dump to the pixel: `(int)(126 × 189/213) = 111` and `(int)(126 × 152/180) = 106`.

## Defects the pictures found

None in the tile. Nothing was changed by this set beyond moving the bar's rectangle into
`NavTileGrid.HealthBar` (geometry identical) and adding the two dials that made the pictures possible.

Two things worth knowing that are not defects:

1. **The cleared wash is not gated on Reduced Motion** (`cleared_100_reduced`). The bite's arm asks
   `!UiMotion.Reduced`; the cleared wave's arm asks only the Coach gate. Consistent with UiMotion's own
   contract ("pulses still run, because a pulse is a short fade"); recorded here so nobody reads the
   picture as a leak.
2. **The forge page's title is hidden behind the currency chain** in every `bar_forge_*` frame. That is
   the chrome overrun already escalated in `production/qa/evidence/attention/README.md` (defect 1),
   not anything this tile does.
