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
#        buildtree buildzoom itemmenu runlog fightgear fightfilter traitlit intro
#
# `fight` takes a third argument: seconds into the wave to pose (default: about one).
# The fixture's creatures are thick on purpose, so a bar only visibly moves a few
# seconds in — pass 6 to photograph a damage number beside a bar it has drained.
# `fightfilter` is the fight with the CHEST FILTER popover open and a setting in it.
#
# `buildzoom` takes a third argument: the tree camera's zoom. With none it poses the
# FIRST-OPEN framing — the centre node and ring 1, which is what a player sees the
# first time — so `buildzoom` alone photographs the first visit. The node art is
# under thirty pixels across in the whole-tree overview, where a capture can only
# prove that something was drawn there — 0.40 frames a capstone medallion with its
# name, 0.95 the minors around the hub.
#
# `intro` takes a third argument too: which card of the first-run intro to pose,
# counted from 1 (default 1). The eight cards each spotlight a different region of
# the HUNT screen, and the only way to know the light frames the right thing is to
# look.
#
# `tour` poses any screen's first-visit tour — the same spotlight walkthrough as the
# intro, one per screen. Third argument: the screen (an Activity name — Stats, Gear,
# Build, Mastery, Vault, Forge, Warren, Map, Traits, Roster; Hunt is the intro).
# Fourth: the card, counted from 1 (default 1). The screen is dressed by the same
# fixture its plain capture uses, so the light falls on real content:
#
#   bash tools/asset-pipeline/capture.sh tour shot_tour_stats_1.png Stats 1
#
# `vaultfirst` poses a NEW GAME's vault — the one welcome gift SeedNewGame parks —
# which is the state the Vault's tour describes. `gemtour` poses the first-gem lesson:
# the forge fixture plus loose gems, with the lesson owed; its fourth argument is
# unused, so pass the card through RH_SHOT_STEP:
#
#   RH_SHOT_STEP=2 bash tools/asset-pipeline/capture.sh gemtour build/shots/gemtour_2.png
#
# `settingsopen` poses the SETTINGS panel with a dropdown list OPEN, so the list's own
# chrome -- its frame, its rows, its mark on the current value -- is photographable rather
# than described. RH_SHOT_DROPDOWN picks WHICH list: `mode` (the default) or `size`, the
# WINDOW SIZE list, whose entries depend on the desktop the capture runs on:
#
#   RH_SHOT_DROPDOWN=size bash tools/asset-pipeline/capture.sh settingsopen build/shots/sizes.png
#
# RH_SHOT_EXPLAIN=Stats,Map (any Activity names, SkillSlot2..4, or FirstGem) leaves those
# screens' tours (or slot notes) OWED under the rig, which otherwise treats everything
# as already explained so no tour sits over the thing a fixture is photographing.
# `tour` sets this for its own screen by itself.
#
# `dust` takes the same third argument for the TRAIT tree's camera (its home zoom
# is about 0.55, fitting the whole tree; 1.2 frames one road with its art readable).
MODE="${1:-fight}"
OUT="${2:-shot_$MODE.png}"
ZOOM="${3:-}"
# `tour`'s third argument is a screen, not a dial — it must not reach RH_SHOT_ZOOM /
# RH_SHOT_T, which the screen's own fixture may read as a zoom or a pose time.
TOUR_TAB=""; TOUR_STEP=""
if [ "$MODE" = "tour" ]; then TOUR_TAB="${3:-Stats}"; TOUR_STEP="${4:-1}"; ZOOM=""; fi
. "$(dirname "${BASH_SOURCE[0]}")/../shellenv.sh" || exit 1
cd "$(dirname "${BASH_SOURCE[0]}")/../.." || exit 1
WINDIR="$(winpath "$PWD")"
# BUILD FIRST. Asset PNGs are copied into the output directory as a build step, so a --no-build run
# renders whatever art was there last time. That silently verified a stale portrait once; a three
# second build is much cheaper than trusting a screenshot that lies.
dn build src/IdleXIdle.Game -v q --nologo >/dev/null 2>&1 \
  || { echo "build failed" >&2; exit 1; }
# RH_SHOT is read by a Windows process, so it must be a Windows path even when this shell
# is using /c or /mnt/c. OUT stays repo-relative — an absolute one fails with a bare
# "capture failed", because the check at the bottom looks for it relative to the repo.
RH_ENV=(RH_SHOT="$WINDIR\\$OUT" RH_SHOT_MODE="$MODE")
# `settingsopen`: which dropdown list to pose. Exported by the caller, forwarded here.
[ -n "$RH_SHOT_DROPDOWN" ] && RH_ENV+=(RH_SHOT_DROPDOWN="$RH_SHOT_DROPDOWN")
# The third argument means "zoom" to buildzoom and "seconds into the flourish" to
# traitlit/traitterm — both are the one dial that mode's capture needs.
[ -n "$ZOOM" ] && RH_ENV+=(RH_SHOT_ZOOM="$ZOOM" RH_SHOT_T="$ZOOM")
# `tour`: the screen and the card. The game resolves the mode to that screen's own fixture.
[ -n "$TOUR_TAB" ] && RH_ENV+=(RH_SHOT_TAB="$TOUR_TAB" RH_SHOT_STEP="$TOUR_STEP")
dn run --project src/IdleXIdle.Game --no-build >/dev/null 2>&1
[ -f "$OUT" ] && echo "captured $OUT ($(stat -c%s "$OUT") bytes)" || { echo "capture failed" >&2; exit 1; }
