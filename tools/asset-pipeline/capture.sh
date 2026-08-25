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
#        buildtree buildzoom itemmenu runlog fightgear traitlit intro
#
# `buildzoom` takes a third argument: the tree camera's zoom (default 0.95). The
# node art is thirty pixels across in the 0.30 overview, where a capture can only
# prove that something was drawn there — 0.55 frames the mastery plaques, 0.95 the
# minors around the hub.
#
# `intro` takes a third argument too: which card of the first-run intro to pose,
# counted from 1 (default 1). The eight cards each spotlight a different region of
# the HUNT screen, and the only way to know the light frames the right thing is to
# look.
#
# RH_SHOT_EXPLAIN=Stats,Map (any Activity names, or SkillSlot2..4) leaves those
# screens' first-open banners OWED under the rig, which otherwise treats everything
# as already explained so no banner sits over the thing a fixture is photographing.
MODE="${1:-fight}"
OUT="${2:-shot_$MODE.png}"
ZOOM="${3:-}"
. "$(dirname "${BASH_SOURCE[0]}")/../shellenv.sh" || exit 1
cd "$(dirname "${BASH_SOURCE[0]}")/../.." || exit 1
WINDIR="$(winpath "$PWD")"
# BUILD FIRST. Asset PNGs are copied into the output directory as a build step, so a --no-build run
# renders whatever art was there last time. That silently verified a stale portrait once; a three
# second build is much cheaper than trusting a screenshot that lies.
dn build src/ResonanceHunter.Game -v q --nologo >/dev/null 2>&1 \
  || { echo "build failed" >&2; exit 1; }
# RH_SHOT is read by a Windows process, so it must be a Windows path even when this shell
# is using /c or /mnt/c. OUT stays repo-relative — an absolute one fails with a bare
# "capture failed", because the check at the bottom looks for it relative to the repo.
RH_ENV=(RH_SHOT="$WINDIR\\$OUT" RH_SHOT_MODE="$MODE")
# The third argument means "zoom" to buildzoom and "seconds into the flourish" to
# traitlit/traitterm — both are the one dial that mode's capture needs.
[ -n "$ZOOM" ] && RH_ENV+=(RH_SHOT_ZOOM="$ZOOM" RH_SHOT_T="$ZOOM")
dn run --project src/ResonanceHunter.Game --no-build >/dev/null 2>&1
[ -f "$OUT" ] && echo "captured $OUT ($(stat -c%s "$OUT") bytes)" || { echo "capture failed" >&2; exit 1; }
