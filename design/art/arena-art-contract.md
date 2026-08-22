# Arena Art Contract — the 2026-08-22 consistency pass

> **Status**: AUTHORITATIVE for every figure and effect drawn on the HUNT stage (champion, enemies,
> bosses, combat VFX). Supersedes the geometry parts of `production/qa/art-consistency-audit.md`
> (the audit that asked for this pass) and the arena rows of `asset-integration-spec.md`.
> Generated through the PixelLab MCP tools; the post-processing lives in `tools/asset-pipeline/v2/`.

## 0. Why one contract

The previous art mixed three cameras (front-on painterly roster, 3/4 pixel enemies, front-on
symmetrical bosses), three scales, and two facings — the champion's clips wound up away from the
enemies and had to be mirrored at draw time. Playtest (2026-08-19, item 8): "perspektif, animasyon
ve boyut tutarsızlıkları var." Every rule below removes one of those degrees of freedom.

## 1. Camera and facing (the non-negotiables)

| Rule | Value |
|---|---|
| Camera | PixelLab `view="side"` (eye level), **three-quarter toward the viewer** |
| Champion | generated **SOUTH-EAST** rotation → **faces RIGHT**, natively, no mirroring |
| Enemies + bosses | generated **SOUTH-WEST** rotation → **face LEFT**, natively |
| Image-route creatures | prompted "three-quarter side view facing left"; verified by eye on the sheet |
| Code | `SoloExpeditionScreen.ArtFacesLeft = false` — the champion is drawn un-flipped |

A frame that faces the wrong way is a failed asset. The sheet command exists so a human looks at
every strip before it is filed; nothing is filed unseen.

## 2. Size chart

On-screen height is decided by the renderer's boxes (it top-crops the headroom and fills the box
height, bottom-anchored), so the chart is in BOXES plus the pixel density that keeps the whole
stage reading as one drawing:

| Figure | Box / target | Source size (PixelLab) | Frame in strip | Density |
|---|---|---|---|---|
| Champion | 430 px (ChampBox) | character size **128** | 512 | ~3.4 px/px |
| Normal enemy | 440 px × archetype scale (0.58 swarm … 1.12 bruiser) | character **128** / image **192–256** | 512 | ~3.4 |
| Boss | 540 px body | character **160** | 512 | ~3.4 |
| Effect | played at scale 1–3 | pixen **256** | 512 | — |

Every strip is **8 square 512-px frames in one row** (`*_strip8_512.png`); the renderer derives the
frame count from `width / height`. Bosses moved from `_strip8_1024` to the same 512 contract.

## 3. Clips

| Owner | Clips | Notes |
|---|---|---|
| Champion (each of the ten) | `idle`, `attack`, `cast`, `death` | idle loops, the others play once on the combat beat; `cast` plays on a Skill event, `death` on the fall |
| Enemy | `idle`, `attack` | the stone sentinel's `slam` is gone — every enemy's clip is `attack` |
| Boss | `idle`, `attack` | |
| Effect | one 8-frame strip | authored white / pale so the Source tint at play time carries the colour |

Clip prompts: idle = "standing idle, feet planted and never moving, only breathing … returns to the
start by the last frame so it loops"; attack = the figure's own verb toward the front, returning
to the start by the last frame; cast = raise a hand, gather glow, release forward; death = stagger,
knees, collapse forward, fully fallen by the last frame.

## 4. Naming (unchanged where the code already asks)

| Asset | Path | Key |
|---|---|---|
| Champion clip | `assets/art/Animations/Roster/<id>_<clip>/` | `char_<id>_<clip>_strip8_512` |
| Champion still | `assets/art/Characters/Roster/` | `char_<id>_base` (the SE rotation, 512²) |
| Enemy clip | `assets/art/Animations/Enemies/<key>_<clip>/` | `<key>_<clip>_strip8_512` |
| Enemy still | `assets/art/Enemies/enemies/<key>/` | `<key>_idle_01`, `<key>_attack_01` (frame 0) |
| Boss clip | `assets/art/Animations/Bosses/<key>_<clip>/` | `<key>_<clip>_strip8_512` |
| Effect | `assets/art/VFX/<name>/` | `fx_<name>_strip8_512` |

Enemy keys (one per Source): bonecrawler (Body), soul_leech (Mind), wisp (Nature),
stone_sentinel (Machine), shadeling (Shadow), rift_guardian (Spirit).
Boss keys (one per region): thorn_regent, forge_colossus, void_reaper, crystal_lich, lumen_angel,
spirit_matron.

## 5. Effects (the skill layer)

Per Form, played on the enemy row unless noted, tinted by the casting skill's **Source** colour:

| Key | What | Where |
|---|---|---|
| `fx_strike` | a crescent slash arc sweeping across | enemy |
| `fx_projectile` | a bolt streaking left→right into a small burst | enemy |
| `fx_aura` | an expanding ring pulse | champion |
| `fx_trap` | a ground burst of shards erupting upward | enemy |
| `fx_mark` | a sigil that flashes and locks | enemy |
| `fx_transformation` | an upward surge of light | champion |

Combat beats: `fx_hit` (spark), `fx_weakhit` (puff), `fx_crit` (starburst), `fx_death` (ash plume),
`fx_heal` (rising motes), `fx_shield` (dome flash), `fx_levelup` (column of light).

## 6. Generation recipe (what the agents ran)

* Humanoids — `create_character(mode="v3", view="side", size=128|160, outline="single color black
  outline")`, then `animate_character(mode="v3", directions=[SE|SW], frame_count=8,
  keep_first_frame=false, action_description=…)`. Frame URLs:
  `…/animations/<animId>/<dir>/<0..7>.png` (read from `get_character`).
* Non-humanoid creatures and every effect — `create_image_pixen(no_background=true, view="side",
  direction="west")` for frame 0, then `animate_image(first_frame_url=…, frame_count=8)`; frames
  `download?index=1..8` (index 0 is the input).
* Assembly — `tools/asset-pipeline/v2/clip.py` (fetch → union-bbox strip → gate → sheet). The
  union bounding box across frames keeps the motion AND keeps the feet on one row, which is what
  `UiKit.AnimSprite` assumes when it measures a single bottom pad for the whole clip.

## 7. Quality gate (`rhart.py gate`)

8 square frames; no empty frame; no disconnected blob above 1.5 % of the figure; scale drift
≤ 12 % (≤ 30 % for attack/death/cast, whose silhouettes legitimately change); baseline drift
≤ 6 % of a frame. Then the sheet, then a human.
