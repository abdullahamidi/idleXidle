#!/bin/bash
# Capture a real screenshot of the game, headless.
#
#   bash tools/asset-pipeline/capture.sh [mode] [outfile]
#
# The game supports RH_SHOT=<path> (render 60 frames, save the canvas, exit) and
# RH_SHOT_MODE=<screen> to skip the title and open a screen directly. That makes
# the render loop verifiable from here instead of guessed at — every layout claim
# should be checked against this, not against the offscreen compositor.
#
# Modes: fight boss expedition forge build character stats warren map dust world
#        region2 region3 conquered lootforge reforge vow hybrid rig vfx help
#        buildtree buildzoom itemmenu runlog fightgear traitlit
#
# `buildzoom` takes a third argument: the tree camera's zoom (default 0.95). The
# node art is thirty pixels across in the 0.30 overview, where a capture can only
# prove that something was drawn there — 0.55 frames the mastery plaques, 0.95 the
# minors around the hub.
MODE="${1:-fight}"
OUT="${2:-shot_$MODE.png}"
ZOOM="${3:-}"
cd "$(dirname "${BASH_SOURCE[0]}")/../.." || exit 1
WINDIR="$(wslpath -w "$PWD")"
# BUILD FIRST. Asset PNGs are copied into the output directory as a build step, so a --no-build run
# renders whatever art was there last time. That silently verified a stale portrait once; a three
# second build is much cheaper than trusting a screenshot that lies.
cmd.exe /c "cd /d $WINDIR && dotnet build src\\ResonanceHunter.Game -v q --nologo" >/dev/null 2>&1 \
  || { echo "build failed" >&2; exit 1; }
# The third argument means "zoom" to buildzoom and "seconds into the flourish" to
# traitlit/traitterm — both are the one dial that mode's capture needs.
ZOOMSET=""
[ -n "$ZOOM" ] && ZOOMSET="set RH_SHOT_ZOOM=$ZOOM&& set RH_SHOT_T=$ZOOM&& "
cmd.exe /c "cd /d $WINDIR && set RH_SHOT=$WINDIR\\$OUT&& set RH_SHOT_MODE=$MODE&& ${ZOOMSET}dotnet run --project src\\ResonanceHunter.Game --no-build" >/dev/null 2>&1
[ -f "$OUT" ] && echo "captured $OUT ($(stat -c%s "$OUT") bytes)" || { echo "capture failed" >&2; exit 1; }
