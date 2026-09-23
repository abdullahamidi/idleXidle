# Composite projectile pilot — `fx_seeker_projectile` (2026-09-23)

| Field | Value |
|---|---|
| **Branch** | `fix/vfx-fade` (not merged) |
| **Scope** | the Seeker's projectile only |
| **Decision record** | `docs/architecture/ADR-010-composite-projectiles-and-the-body-size-contract.md` (Proposed) |
| **Status** | awaiting the owner's verdict; no other projectile has been touched |

## How it was filmed

- **The cast.** The Seeker's SPRAY in the HUNT fixture, true speed. The capture rig takes one picture
  per 16.7 ms game step (`capture_seq.sh fight 72 1`, `RH_SHOT_HUNTER=seeker RH_SHOT_T=4.1`).
- **The colour.** The fixture weaves SPRAY in Mind (cyan). The films pose it in **Body**, which is the
  Seeker's red/pink, with the new `RH_SHOT_SWAP=volley_spray:volley_spray@Body`.
- **The three versions.** All three come from the same build through the same fight:
  - `old`: the pre-cohort strip (`01a401b`);
  - `tumble`: the unapproved PixelLab pilot (`7ea56119`);
  - `new`: the composite.
  The old and the tumbling strip were drawn with `RH_VFX_COMPOSITE=0`.
- **Alignment.** The films are aligned on the hunter's own pose, so "+ms" is time since the cast in all
  three.
- **Real speed.** The GIFs show every 2nd game frame at 30/30/40 ms.

| File | What it shows |
|---|---|
| `01_world_three_way_realspeed.gif` | the arena as the player sees it, all three stacked |
| `02_follow_three_way_realspeed.gif` | a window that rides the projectile's own path, so world motion cancels and only its own motion is left |
| `03_follow_three_way_sheet.png` | the follow view every 4th game frame |
| `04_world_three_way_stills.png` | six moments in the arena at half size (about combat reading size) |
| `05_impact_closeup_every_frame.png` | the last three frames of flight, contact, and 14 frames of impact and residue |
| `06_repeated_casts_100ms.png` | ten seconds of the fight at 100 ms steps: both casts, and what each leaves behind |
| `07_parts.png` | the six part textures (white; the Source tint colours them) |
| `08_same_composite_body_and_mind.png` | the same knife cast in Body and in Mind: the colour is the Source |

## What changed

The strip is no longer drawn for this key. `VfxPlayer` still spawns, places and times the flight (same
from/to, same ease-out, same 1.0 s clock). `ProjectileVisual` draws it from these layers, dimmest first:

1. **Wake** (tertiary): 0.30 s of the real path; width 0.9 × the head's thickness, tapering with age; a
   small lateral wave; opacity 0.9 falling with age.
2. **Core** (secondary): 0.09 s of the real path, starting at the head's rear edge; width 0.32 × the
   thickness; 50 % toward white.
3. **Sparks:** at most 3, shed from the rear quarter and falling back, 0.22 s each.
4. **Head** (primary): 280 px (0.68 × the 412 px hunter), a ±6° wobble at 2.2 Hz, no spin. For the last
   80 ms before contact it gains a little light and the core tightens.
5. **Glint:** one point of light that crosses the blade once, from 0.30 to 0.46 of the flight, rear to
   tip.

The head strikes when its centre is 0.2 × its length from the target's centre. That is 683 ms after the
cast, where the old strip began its tail fade. The impact is:

- a 0.09 s flash;
- 8 slim shards, 6 of them inside ±32° of the incoming direction and 2 radial, gone in 0.26 s.

The trail then tapers at its front and dims away in 0.12 s.

**Sizes, from the placement dump.**

| Version | Drawn size |
|---|---|
| Old strip | content 416 × 115 px (its blade reads about 270 px) |
| Tumbling pilot | content 148 × 115 px |
| **Composite head** | **280 px long, fixed for the whole flight** |

**Evaluated and dropped.** Two positional afterimages (35 % and 16 %) overlapped the long blade and
smeared its silhouette. The trail already carries the continuity.

## Acceptance checklist (the brief's list, as filmed)

| Criterion | Where to look | Reading |
|---|---|---|
| silhouette readable | 02, 03 | the same serrated knife in every frame |
| not 2.5× smaller | 04 | 280 px against the old ~270 px blade; the tumbler was 148 px |
| world movement obvious | 01, 04 | the same path and timing as the old strip |
| internal motion | 02, 03 | the trail stretches and shortens with speed; the glint crosses once; small tilt |
| trail evolves | 03 | long while fast and short as it slows, drawn from where the knife really was |
| not an icon sliding | 04 | every still has a streak behind the pommel pointing back along the path |
| not an icon spinning | 03 | ±6° only |
| blade shape stable | 02 | one texture, one scale; the wobble never scales it |
| no popping / rectangle / bloom | 01, 05 | 60 % on the cast frame, full on the next; the landed trail tapers and dims (an early build left a block and was fixed) |
| trail follows the trajectory | unit test | every sample lies on the flight line |
| impact has direction | 05 | the shards fan forward, through the target |
| repeated casts not cluttered | 06 | about 6 s apart in this fixture; each flight's residue is gone ~0.3 s after contact |

## Cost (measured with `RH_VFX_METRICS=1`)

| Measure | Old strip | Composite |
|---|---|---|
| sprites per frame (this projectile) | 1 | ≤ 32 (mean ≈ 18) |
| retained trail points | — | ≤ 32 (a fixed ring) |
| particles | — | 3 spark + 8 shard slots (fixed arrays) |
| bytes allocated per frame | — | **0** (over 441 fight frames, and a unit test) |
| bytes allocated per cast | — | < 2 KB (unit test budget) |
| VFX-pass draw calls, mean / peak over the same fight window | 18.4 / 23 | 19.7 / 24 |

A composite is at most about 5 texture runs. So 10–20 simultaneous projectiles stay inside the planning
budget of "low hundreds of draw calls". Beyond that, draw all composites layer by layer; nothing does
that yet.

## PixelLab

2 generations this pass (4,354 → 4,352):

- **`a3b66867`:** the glint, used.
- **`566fab7e`:** a shard, rejected. It scattered opaque dither over the whole canvas, and the shard is
  now procedural.

The knife head reuses the cohort knife `cd704393`, so no new shape was generated.

## Gates

| Check | Result |
|---|---|
| build | `-warnaserror` 0 warnings / 0 errors |
| tests | Core 1,850 · Game 732 (20 new, in `vfx_projectile_composite_test.cs`) · Integration 2 |
| `check_all.sh` | green |
| asset-key and consumer gates | green |
| `check_fx_edges` library count | 57 of 67 failing (was 56). The reinstalled pre-cohort fallback strip fails SOFT, where the unapproved tumbler passed. The fallback is never drawn while the composite is on. |

## Known and unchanged

- **The ease-out decelerates the knife into the target**, which weakens the sense of momentum. Changing
  it changes the travel timing, which this pass was told not to touch.
- **The creature's hit flash plays at cast time**, about 0.55 s before the knife arrives (the sim, not the
  VFX).
