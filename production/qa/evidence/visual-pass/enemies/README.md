# The enemy matrix, photographed — 2026-09-16

Phase 3 of the visual identity pass. The arena used to draw one body per Source and tell a region's
four roles apart only by drawing that one body bigger or smaller. Now `EnemyPresentation` (Game)
chooses the body from the REGION and the role the run rolled: six regions by four archetypes,
twenty-four ordinary creatures, plus the six region bosses. Presentation only: no Core file, stat,
roll or save field changed.

Re-run everything with:

```
bash tools/asset-pipeline/enemy_fixtures.sh [filter]                      # the live arena captures
python tools/asset-pipeline/v2/matrix_sheet.py <out.png> [--frame idle|attack|death] [--index n]
python tools/asset-pipeline/v2/rhart.py sheet <out.png> assets/art/Animations/Bosses/*_attack/*.png
```

## The matrix

| Region | Swarm | Caster | Armoured | Bruiser | Boss |
|---|---|---|---|---|---|
| Verdant Hollow | BRIAR MITE | BOG WEAVER | BARK WARDEN | THORN OGRE | THORN REGENT |
| Cinderworks | CINDER GNAT | FURNACE PRIEST | BOILER KNIGHT | SLAG HULK | FORGE COLOSSUS |
| Umbral Reach | GLOOM WHELP | HOLLOW SEER | NIGHT CARAPACE | DUSK APE | VOID REAPER |
| Marrow Wastes | MARROW TICK | CARRION SHAMAN | SHELL GHOUL | FLAYED BRUTE | SPIRIT MATRON |
| The Still Archive | GLYPH MOTE | LENS ARCHIVIST | RUNE SENTRY | TABLET GOLEM | CRYSTAL LICH |
| The Pale Choir | CHOIR WISP | PALE CANTOR | HALO GUARD | BELL GIANT | LUMEN ANGEL |

Keys are `<slug>_<archetype>` (`verdant_swarm` ... `choir_bruiser`). Every body faces LEFT (the
south-west rotation) and ships idle, attack and death as 8 x 512 strips, plus `_idle_01` and
`_attack_01` stills cut from frame 0. Swarm bodies come from the image route (pixen 192 px +
`animate_image`); the other three roles from the character route (128 px, v3 animation). The
prompts, the PixelLab id of every kept clip and every rejected candidate are in
`tools/asset-pipeline/v2/spec.json` under `enemy_matrix`.

## Contact sheets

`contact_sheet_idle.png`, `contact_sheet_attack.png` (frame 4, the swing) and
`contact_sheet_death.png` (frame 7, the end) lay the six regions against the four roles, with each
region's boss in a fifth column. Each body is drawn the way the arena sizes it: scaled by its own
headroom, bottom-anchored, in a box at `HuntScreen.ArchetypeScale` of the Bruiser's.

What the sheets say:

- **Each region reads as one family** by palette and material. Verdant is moss, bark and thorn;
  Cinderworks is iron with furnace orange; Umbral is near-black with violet; Marrow is bone and red
  sinew; the Archive is pale stone and cyan crystal; the Choir is white, lilac and bronze.
- **Each role reads from its silhouette** at arena size. Swarms are low and wide (a beetle, a gnat,
  a whelp, a tick, a shard cluster, a wisp). Casters are tall and thin, robed or hooded, with a staff
  or lenses. Armoured bodies are closed and plated with their weight low (a shield, a boiler, a
  carapace, a bone shell, crossed crystal arms, a tower shield with wings). Bruisers are broad (an
  ogre, a slag hulk, an ape, a flayed brute, a tablet golem, a bell giant).
- **Source stays readable without being in the body.** The wave strip shows every Source the wave
  carries, and the inspector wears the hovered creature's own glyph and accent. No body carries a
  Source mark; the one restrained cue is the region's palette, which matches its theme Source.
- **Closest pairs, kept.** The Slag Hulk and the Forge Colossus are both lava-seamed iron hulks; a
  boss wave stands alone, at boss scale, under its own name bar, so the two never share a screen.
  The Tablet Golem and the Rune Sentry share crystal plating but not a stance (broad and upright
  against crouched with crossed arms).
- **Motion the image route cannot give.** The Glyph Mote's and the Choir Wisp's idles barely move;
  `animate_image` only ever produces subtle motion on a still. They read, but they breathe less than
  the character-route bodies.
- **Attack flashes.** The Rune Sentry's clap lights its whole body cyan for four frames; it reads as
  the strike charging and stays in the Archive's palette, so it was kept.

## Re-rolled on a concrete defect

| Cell | Clip | Defect | Fix |
|---|---|---|---|
| verdant_caster, verdant_armoured | base | v1 bases, replaced at the Verdant pilot review | v2 bases |
| archive_caster | base | a halo: read as the Pale Choir | v2 base |
| archive_caster | idle | a blue flame grew on the head over frames 5-8, so the loop popped | re-rolled with "no new light" |
| archive_armoured | base | a human face under the helm | v2 base |
| archive_swarm | death | the shards scattered to a thin line (4.8 % filled); the second try drew the body at 0.83 on death | third try: a compact pile, 0.97 |
| choir_bruiser | base | an olive body, off the Choir palette | v2 base |
| marrow_caster | attack | the whole figure turned to orange flame, the Cinderworks palette | re-rolled in bone and red; the thrown shards are allowed as a second blob for that clip only |
| marrow_swarm | death | the shell drained to white and the tick never fell | re-rolled: the legs buckle and it collapses |
| verdant_swarm | death | almost no motion, so the first kill a new player makes read as a freeze; a prompt-only retry stayed upright | an end pose on its back (edit_image_pixen), interpolated to |

Every kept clip passes `matrixclips.py`: the rhart gate per clip (tight for idle, loose for attack,
loose plus the fallen rule for death) and a body-size jump of at most 12 % between a clip and its idle.
Across the 48 non-idle clips the kept jumps run from 0.90 (cinder_swarm) to 1.05 (umbral_swarm).

## Boss audit

`boss_audit_attack.png` and the fifth column of the three contact sheets. All six bosses face left,
are 8 x 512, and read against their region's family.

| Boss | Verdict |
|---|---|
| Thorn Regent | kept |
| Forge Colossus | **attack re-rolled.** At the impact the body turned into a translucent smoke outline for two frames. The new attack stays solid, with sparks at the fist only (0.91 of the idle's size; the old one was 0.92). Idle and death kept. |
| Void Reaper | kept |
| Spirit Matron | kept |
| Crystal Lich | kept |
| Lumen Angel | kept |

## In the live arena

`bash tools/asset-pipeline/enemy_fixtures.sh` takes 62 captures from the running game (`fight` /
`fightinspect`), each posed with `RH_SHOT_SOURCE` (the family of the region whose theme that Source
is) and `RH_SHOT_ARCHETYPE` (the body), at a count the role can really roll (`RH_SHOT_CREATURES`:
Swarm as rolled, Caster and Armoured two, Bruiser one).

| Files | What they pose |
|---|---|
| `cell_<region>_<role>_100` | all 24 cells |
| `verdant_*`, `cinder_*`, `choir_*` at 100 / 125 / 150 | the starting family, the second and the last: a Swarm pack, a lone Bruiser, a Caster pair biting (`RH_SHOT_BITE=1`, `.events.txt` beside it), an Armoured pair |
| `inspect_verdant_100`, `inspect_choir_150` | the hover inspector over a Caster |
| `capped_marrow_bruiser_{100,150}` | the capped-actor geometry pose (`.actors.txt`: box 488 x 492, MAGNIFIED) |

What the pictures say:

| Check | Verdict |
|---|---|
| Every cell draws its own region's body for its own role, facing the Hunter, feet on the plane | PASS (24 of 24) |
| The four roles of one region read apart at arena size: the pack low and wide, the casters tall, the armoured pair closed and heavy, the Bruiser alone and broad | PASS |
| The wave strip names the role drawn (SWARM, CASTER, ARMOURED, BRUISER) and shows the Source glyph the wave carries | PASS |
| The lone creature's nameplate is the catalogue's name (THORN OGRE, SLAG HULK, BELL GIANT) | PASS |
| The Caster pair's attack clip plays on the bite: staff thrusts, flame swings, the Pale Cantor's light burst | PASS |
| The inspector's name line is the creature's own (BOG WEAVER, PALE CANTOR), with its role and its own Source glyph in the corner, at 100 and 150 | PASS |
| The capped pose still reaches the renderer's ceiling with the new Bruisers | PASS |
| **The lone creature's name and bar were painted over by the IDLE panel** at 100, 125 and 150 (a tall Bruiser raises its plate into the band the right rail covers, and the rail is drawn after the arena) | **FOUND AND FIXED**: the plate now slides left of the rail panel it would sit under (`HuntScreen.DrawNormalEnemy`); the `*_lone_*` captures were re-shot after the fix |

Fixture notes, not defects:

- The header names the fixture save's region (Umbral Reach) whatever family is posed; only the art and
  the wave glyph follow the pose.
- `N OF M STANDING` counts the wave the save rolled; `RH_SHOT_CREATURES` only limits what is drawn.
- The fixture save sits above corruption tier 0, and every corrupted tier tints enemies violet
  (`CorruptionLook`), so the Pale Choir's white reads pink-lilac here. At tier 0 the tint is white.
- Two-creature rows overlap by design (the row layout packs its boxes); the silhouettes stay separate.
- A damage number can cross a nameplate for a few frames (`cinder_lone`), as it always could.

## Memory

Every animation strip is 8 MB resident. The strips, the enemy stills and each champion's own effects
now load on first use (`AssetLibrary.IsDeferred`), warmed from Update by `HuntScreen.WarmArt`.
`tools/asset-pipeline/texture_budget.py` prices it:

| | MB |
|---|---|
| every file decoded (the old eager loader, with this art) | 2247 |
| resident at boot | 439 |
| one fight: boot plus one champion, one region family, one boss | 671 |
| measured in the running game, a posed fight (`*_cast_*.png.assets.txt`: 457 textures loaded, 232 still indexed) | 679 |

The live figure is written beside any `RH_SHOT_DUMP` capture as `<shot>.assets.txt`. A fight is still
above the 512 MB working ceiling; what remains eager at boot is mostly the shared effect strips
(144 MB) and the environment plates (138 MB).
