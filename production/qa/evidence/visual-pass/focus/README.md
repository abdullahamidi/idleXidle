# The focus light, photographed — 2026-09-16 (re-shot 2026-09-17 on the new enemy art and item frames)

Phase 1 of the shape-aware tutorial lighting: the page darkens and the one thing a card is about is
left lit, cut to its own shape — a figure by its silhouette, a control by a soft rounded plate with a
faint ivory rim — instead of framed by corner brackets. Every lit shape family at 100 / 125 / 150,
plus one filmstrip, looked at rather than reasoned about.

What did NOT change is the point of the pass: the rectangle a step lights for the click and for the
card (`Game1.TourSpotlights`, `HuntScreen.Spotlights`, `ClickableOf`, `TourCardRect`) is the same
rectangle it was, pinned as literals at all three densities in
`tests/unit/IdleXIdle.Game.Tests/focus_shapes_test.cs`. Only the picture is new
(`src/IdleXIdle.Game/FocusShape.cs`, `FocusRenderer.cs`).

## Re-running the whole matrix

```
bash tools/asset-pipeline/focus_fixtures.sh [filter]
```

One command, 36 captures (~35 s each) plus the filmstrip. The filter is a substring of
`<state>_<scale>`, so `boss` retakes the three boss frames and `_150` retakes one profile. Each row is
`<state>|<mode>|<dials>|<arg>` and lands at `production/qa/evidence/visual-pass/focus/<state>_<scale>.png`.

## The matrix

| State | Shape | Fixture | Dials | Files |
|---|---|---|---|---|
| the Hunter's own outline | silhouette | `fight` | `RH_SHOT_OPENING=IntroduceHunter` | `hunter_intro_{100,125,150}` |
| the first wave, posed | silhouette + header plate | `fight` | `RH_SHOT_OPENING=IntroduceEnemy` | `enemy_intro_{100,125,150}` |
| the boss's own outline | silhouette + header plate | `boss` | `RH_SHOT_OPENING=IntroduceBoss` | `boss_intro_{100,125,150}` |
| the pack's own outlines | one silhouette per creature + header plate | `intro` card 2 | — | `enemy_tour_{100,125,150}` |
| the health panel | plate | `fight` | `RH_SHOT_OPENING=IntroduceHealth` | `health_panel_{100,125,150}` |
| the stage header | plate | `fight` | `RH_SHOT_OPENING=IntroduceStage` | `stage_header_{100,125,150}` |
| the VAULT rail tile | plate | `fight` | `RH_SHOT_OPENING=IntroduceChest` | `vault_tile_{100,125,150}` |
| the chest's card | plate | `vaultfirst` | `RH_SHOT_OPENING=ForceChestOpen` | `chest_{100,125,150}` |
| one bag cell | plate | `character` | `RH_SHOT_OPENING=ForceItemSelect` | `gear_cell_{100,125,150}` |
| the item detail panel | plate | `character` | `RH_SHOT_OPENING=ExplainItem` | `item_detail_{100,125,150}` |
| the training rows (coach) | plate | `stats` | `RH_SHOT_LESSON=FirstTrainingPurchase` | `training_rows_{100,125,150}` |
| the mastery tree (coach) | plate | `buildtree` | `RH_SHOT_LESSON=FirstMasterySpend` | `mastery_target_{100,125,150}` |
| the light following the idle | silhouette, 16 frames 100 ms apart | `capture_seq.sh fight 16 6` | `RH_SHOT_OPENING=IntroduceHunter` | `filmstrip_hunter_100` |

The constants the pictures were taken with (`FocusRenderer.cs`): plate radius 16 px, plate feather
12 px, silhouette feather 7 px at 0.35 per copy, halo reach 10 px at 0.14 per copy (ivory
`F2E9D6`), rim 1.5 px at 0.35 alpha, scrim built at half resolution. Weights are the orchestrators'
own: coach 0.66 × blaze, tour 0.74, opening 0.80 × blaze.

## What the pictures said

| Picture | What it shows | Verdict |
|---|---|---|
| `hunter_intro_*` | The Hunter cut out of the scrim by his own outline: hood, cape and the sword arm all inside the light, the edge soft over ~7 px, a faint pale rim just outside the figure. No rectangle anywhere. The card sits to his right at all three densities and never touches him. The rest of the HUNT — the HUD, the header, the dock — is under the 0.80 scrim. | PASS |
| `hunter_intro_100` zoomed (600–720 × 470–560) | The feather is a smooth ramp, not a stepped dilation; the hood's edge keeps its pixels; nothing of the figure is clipped. | PASS |
| `enemy_intro_*` | **Re-shot 2026-09-17 on the region x archetype cast.** The posed beat still parks the fight before the host pushes the region, but the catalogue now draws the STARTING region's family for an unset region (`EnemyPresentation.For`), so the lone creature is a BOG WEAVER, not the ember block: cut out by its own outline with the faint rim, the header plate lit beside it, its dimmed name and bar left under the scrim. (The header still names the fixture save's region, Umbral Reach; a fresh save opens in Verdant Hollow, where this family belongs.) | PASS |
| `boss_intro_*` | The crystal lich cut out by its own outline, the floating shards included, over a soft ivory glow — the cinematic frame the pass was for. The header plate is lit with it. At 150 the card's top edge lands a few pixels under the lich's feet, still clear of the figure (checked at 1:1, `boss_intro_150` 1000–1920 × 700–1080). | PASS |
| `enemy_tour_*` | **Re-shot 2026-09-17.** Four BRIAR MITES (Verdant Hollow's swarm), each its own silhouette: legs and thorns intact, no bridging rectangle across the overlapping row, with the header plate. At 150 the tour card is placed below the band and overlaps the skill dock's left edge, which is `TourCardRect`'s existing choice and not part of this pass. | PASS (note) |
| `enemy_tour_100` zoomed (1050–1650 × 560–900) | Each silhouette's glow follows the flames; no halo bleeds from one wisp into the next. | PASS |
| `health_panel_*` | The hunter's HUD card lit as a plate with a rim; the card hangs under it. The running fight is under the scrim. | PASS |
| `stage_header_*` | The header plate alone, the pack and the Hunter dark under it; the card below. | PASS |
| `vault_tile_*` | The VAULT rail tile lit as a small plate; the card beside it on the page. The tile is the forced click's control and the plate is its rectangle. | PASS |
| `chest_*` | The welcome chest's card as one plate, the OPEN control inside it; the card under it at 100 / 125 and at the page's bottom right at 150, never over the plate. | PASS |
| `gear_cell_*` | **Re-shot 2026-09-17 on the enlarged rarity frame** (94 % of the cell). One bag cell as a small plate with a rim, the whole frame inside the light, the rest of the GEAR page dark; the card beside it, clear of the cell. | PASS |
| `item_detail_*` | The item detail panel as one tall plate, its EQUIP button inside; the card to its left. | PASS |
| `training_rows_*` | The coach on TRAINING: the rows panel as one large plate, the inspector and the rail under the 0.66 scrim, the lesson card in the toast lane below. | PASS |
| `mastery_target_*` | The coach on MASTERY: the tree panel as one large plate; the rail and the inspector dimmed. The plate is most of the page, so the scrim reads as a border. | PASS |
| `filmstrip_hunter_100` | Sixteen frames, 100 ms apart, of the paused `IntroduceHunter` beat: the Hunter's idle plays and the silhouette light follows every pose without a flicker; the card and the scrim hold still. | PASS |

## Defects and notes

- **RESOLVED 2026-09-17: the posed `IntroduceEnemy` beat had no creature art.** The rig parks the beat
  before the host pushes the region; the enemy catalogue now draws the starting region's family for an
  unset region, so the beat photographs a real creature lit by its silhouette.
- **The tour card at 150 sits on the skill dock** (`enemy_tour_150`, `boss_intro_150`). `TourCardRect`
  keeps the card off the holes, and the `avoid` list only holds the chrome row plus the dock for the
  HUNT tour — it is pre-existing placement, untouched here.
- **A silhouette has no marker after a blaze.** A live opening beat's scrim fades after 3.2 s and the
  coach's after 6 s; a plate keeps its faint rim, a silhouette keeps nothing, so the card stands alone
  over the running fight. The brief asked for no brackets; whether a figure wants a resting marker is
  a design question for Phase 2.
- `Ellipse` is built (a soft disc) and untested by any production target, since nothing lights one yet.
