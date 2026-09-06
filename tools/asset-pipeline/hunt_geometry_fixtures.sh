#!/bin/bash
# THE ACTOR GEOMETRY POSES (2026-09-06), at 100 / 125 / 150 — the fixture is this recipe. Every capture
# carries the geometry overlay (RH_SHOT_GEOMETRY=1: each figure's BODY bright, its ENVELOPE dim, the
# hunter's KEEP-OUT in ember) and writes <shot>.actors.txt beside it (RH_SHOT_DUMP=1: body / envelope /
# keep-out / box per subject, canvas px) — the numbers first, the picture second.
#
#   GEOMETRY   vfxdebug     the seeker, QUIVER (the widest idle) and THE OATHBOUND (the narrowest):
#                           three silhouettes, the keep-out against the old 400x430 box
#   SWING      fightswing   the strike at its apex (0.55) — the blade inside the stable envelope
#   HOVER      fightinspect the pointer at the right-edge creature's envelope edge, PAST its idle body
#                           (a plate) and 15 px further out (no plate). The row drifts ±10 px between
#                           runs with the lunge clock, so the probes sit 10 px inside the edge; read
#                           the .actors.txt of the run before doubting a miss.
#
# Usage: bash tools/asset-pipeline/hunt_geometry_fixtures.sh <tag>
# Writes build/shots/polish/<tag>_*.png (+ .actors.txt, .events.txt) — see polish_shots.sh.
set -u
cd "$(dirname "${BASH_SOURCE[0]}")/../.." || exit 1
TAG="${1:?tag}"
G="RH_SHOT_GEOMETRY=1;RH_SHOT_DUMP=1"
bash tools/asset-pipeline/polish_shots.sh "$TAG" \
  "vfxdebug:100:RH_SHOT_DUMP=1" "vfxdebug:125:RH_SHOT_DUMP=1" "vfxdebug:150:RH_SHOT_DUMP=1" \
  "vfxdebug:100:RH_SHOT_HUNTER=quiver;RH_SHOT_DUMP=1" "vfxdebug:150:RH_SHOT_HUNTER=quiver;RH_SHOT_DUMP=1" \
  "vfxdebug:100:RH_SHOT_HUNTER=oathbound;RH_SHOT_DUMP=1" "vfxdebug:150:RH_SHOT_HUNTER=oathbound;RH_SHOT_DUMP=1" \
  "fightswing:100:$G;RH_SHOT_SWING=0.55" "fightswing:150:$G;RH_SHOT_SWING=0.55" \
  "fightinspect:100:RH_SHOT_T=7;$G" "fightinspect:125:RH_SHOT_T=7;$G" "fightinspect:150:RH_SHOT_T=7;$G" \
  "fightinspect:100:RH_SHOT_T=7;$G;RH_SHOT_CANVAS_MOUSE=1730,760" \
  "fightinspect:100:RH_SHOT_T=7;$G;RH_SHOT_CANVAS_MOUSE=1745,760" \
  "fightinspect:125:RH_SHOT_T=7;$G;RH_SHOT_CANVAS_MOUSE=1733,720" \
  "fightinspect:125:RH_SHOT_T=7;$G;RH_SHOT_CANVAS_MOUSE=1748,720" \
  "fightinspect:150:RH_SHOT_T=7;$G;RH_SHOT_CANVAS_MOUSE=1733,700" \
  "fightinspect:150:RH_SHOT_T=7;$G;RH_SHOT_CANVAS_MOUSE=1748,700"
