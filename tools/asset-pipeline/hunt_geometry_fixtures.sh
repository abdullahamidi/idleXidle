#!/bin/bash
# THE ACTOR GEOMETRY POSES (2026-09-06), at 100 / 125 / 150 — the fixture is this recipe. Every capture
# carries the geometry overlay (RH_SHOT_GEOMETRY=1: each figure's BODY bright, its ENVELOPE dim, the
# hunter's KEEP-OUT in ember) and writes <shot>.actors.txt beside it (RH_SHOT_DUMP=1: body / envelope /
# keep-out / box per subject, canvas px, the arena's own hover verdict, and CAPPED or GRANTED per
# actor) — numbers first, picture second.
#
#   GEOMETRY   vfxdebug     the seeker, QUIVER (the widest idle) and THE OATHBOUND (the narrowest):
#                           three silhouettes, the keep-out against the old 400x430 box
#   SWING      fightswing   the strike at its apex (0.55) — the blade inside the stable envelope
#   HOVER      fightinspect the pointer at the right-edge creature's envelope edge, PAST its idle body
#                           (a plate) and 20 px further out (no plate). The row drifts ±10 px between
#                           runs with the lunge clock, so the probes sit 10 px inside the edge; read
#                           the .actors.txt of the run before doubting a miss.
#   CAPPED     vfxdebug     THE ONE ACTOR THE RENDERER SHRINKS. A box may ask for more magnification
#              + fight      than UiKit.RasterCeiling allows, and only the 488x492 box asks it of an
#                           idle as deeply letterboxed as the rift guardian's (126 px of sky): it asks
#                           1.275 and is drawn at 1.25.
#
#                           THAT BOX IS A WAVE OF TWO OR MORE at Bruiser scale — NOT a Bruiser wave.
#                           A Bruiser rolls exactly one creature (Archetypes.Shapes, min 1 max 1) and
#                           a lone creature is laid out by LayoutSingleEnemy at the UNSCALED 436x440,
#                           which never caps. In play the box comes from a NUMBERS band's Bruiser roll;
#                           here RH_SHOT_ARCHETYPE forces the scale and RH_SHOT_SOURCE the figure, but
#                           NEITHER FORCES THE COUNT — that is the wave's own seeded roll. So the game
#                           REFUSES the capture (HuntScreen.RequireArchetypeReached throws) when the
#                           wave it landed on is a single creature or a boss, rather than photographing
#                           an uncapped figure as if the pose had worked.
#
#                           A CAPPED pose is only honoured when its dump shows a creature `box` of
#                           488x492 and a `CAPPED` line. `GRANTED` there means the ceiling was not
#                           reached; if the run did not throw and did not cap, the pose is broken.
#
# Usage: bash tools/asset-pipeline/hunt_geometry_fixtures.sh <tag>
# Writes build/shots/polish/<tag>_*.png (+ .actors.txt, .events.txt) — see polish_shots.sh.
set -u
cd "$(dirname "${BASH_SOURCE[0]}")/../.." || exit 1
TAG="${1:?tag}"
G="RH_SHOT_GEOMETRY=1;RH_SHOT_DUMP=1"
CAP="RH_SHOT_SOURCE=Spirit;RH_SHOT_ARCHETYPE=Bruiser;RH_SHOT_DUMP=1"
bash tools/asset-pipeline/polish_shots.sh "$TAG" \
  "vfxdebug:100:RH_SHOT_DUMP=1" "vfxdebug:125:RH_SHOT_DUMP=1" "vfxdebug:150:RH_SHOT_DUMP=1" \
  "vfxdebug:100:RH_SHOT_HUNTER=quiver;RH_SHOT_DUMP=1" "vfxdebug:150:RH_SHOT_HUNTER=quiver;RH_SHOT_DUMP=1" \
  "vfxdebug:100:RH_SHOT_HUNTER=oathbound;RH_SHOT_DUMP=1" "vfxdebug:150:RH_SHOT_HUNTER=oathbound;RH_SHOT_DUMP=1" \
  "fightswing:100:$G;RH_SHOT_SWING=0.55" "fightswing:150:$G;RH_SHOT_SWING=0.55" \
  "fightinspect:100:RH_SHOT_T=7;$G" "fightinspect:125:RH_SHOT_T=7;$G" "fightinspect:150:RH_SHOT_T=7;$G" \
  "fightinspect:100:RH_SHOT_T=7;$G;RH_SHOT_CANVAS_MOUSE=1730,760" \
  "fightinspect:100:RH_SHOT_T=7;$G;RH_SHOT_CANVAS_MOUSE=1750,760" \
  "fightinspect:125:RH_SHOT_T=7;$G;RH_SHOT_CANVAS_MOUSE=1730,700" \
  "fightinspect:125:RH_SHOT_T=7;$G;RH_SHOT_CANVAS_MOUSE=1750,700" \
  "fightinspect:150:RH_SHOT_T=7;$G;RH_SHOT_CANVAS_MOUSE=1730,700" \
  "fightinspect:150:RH_SHOT_T=7;$G;RH_SHOT_CANVAS_MOUSE=1750,700" \
  "vfxdebug:100:$CAP" "vfxdebug:125:$CAP" "vfxdebug:150:$CAP" \
  "fight:100:$CAP;RH_SHOT_T=1.2" "fight:125:$CAP;RH_SHOT_T=1.2" "fight:150:$CAP;RH_SHOT_T=1.2"
