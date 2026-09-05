#!/bin/bash
# THE WARREN'S THREE POSES (2026-09-06), at 100 / 125 / 150 — the fixture is this recipe, not a
# coordinate hidden in code. Every hover is posed through the rig's canvas-coordinate dial; a plain
# capture hovers nothing, because the rig parks its cursor outside the canvas (Game1.ParkedCursor).
#
#   WARREN_IDLE     warren        no card hovered; the detail rail shows its hint
#   WARREN_HOVER    warren        the cursor on the first card (NURSERY); the rail says it
#   WARREN_LOCKED   warrenlocked  the grid scrolled to its foot, the cursor on the first LOCKED card
#                                 (RITUAL NEST); the rail says what opens it, from the unlock model
#
# Usage: bash tools/asset-pipeline/warren_fixtures.sh <tag>
# Writes build/shots/polish/<tag>_warren*_<scale>.png (see polish_shots.sh).
set -u
cd "$(dirname "${BASH_SOURCE[0]}")/../.." || exit 1
TAG="${1:?tag}"
bash tools/asset-pipeline/polish_shots.sh "$TAG" \
  "warren:100" "warren:125" "warren:150" \
  "warren:100:RH_SHOT_CANVAS_MOUSE=370,560" \
  "warren:125:RH_SHOT_CANVAS_MOUSE=370,700" \
  "warren:150:RH_SHOT_CANVAS_MOUSE=370,800" \
  "warrenlocked:100:RH_SHOT_GRID_SCROLL=999;RH_SHOT_CANVAS_MOUSE=370,800" \
  "warrenlocked:125:RH_SHOT_GRID_SCROLL=999;RH_SHOT_CANVAS_MOUSE=370,750" \
  "warrenlocked:150:RH_SHOT_GRID_SCROLL=999;RH_SHOT_CANVAS_MOUSE=370,800"
