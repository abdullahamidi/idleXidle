#!/bin/bash
# THE STORE SCREENSHOTS — twelve 1920x1080 captures of the current build, in the order the store page
# shows them (docs/store/store-page.md §5), written to docs/store/screenshots/.
#
#   bash tools/marketing/store_screenshots.sh
#
# Every shot is a capture-rig fixture posed with RH_SHOT_SHOWCASE=1 and RH_SHOT_RAIL=all: a progressed
# account's rail (every screen open), guidance off (no coach cards), no WELCOME BACK toast. The fixtures
# themselves exist to test one state each and carry a fresh account's locks and lessons, which is not
# what a player an hour in sees. Nothing about the fight, the art or the numbers is changed.
# UI SCALE 100 %, the default. The itch.io page (docs/store/itch/README.md) and the Steam page take the
# files as they are; itch has no API for screenshots, so the owner uploads them by hand.
set -u
. "$(dirname "${BASH_SOURCE[0]}")/../shellenv.sh" || exit 1
cd "$(dirname "${BASH_SOURCE[0]}")/../.." || exit 1
DEST="docs/store/screenshots"
SHOW="RH_SHOT_SHOWCASE=1;RH_SHOT_RAIL=all"

# name | fixture | extra dials (after the showcase pair)
SHOTS=(
  "fight|fight|RH_SHOT_HUNTER=quiver"      # QUIVER's volley landing a critical on a swarm
  "region|region2|"                          # the second region: Cinderworks' forge golems
  "boss|corruptedboss|"                      # a boss wave, the region corrupted
  "build|weave|"                             # THE BUILD — skills, keystones, vows
  "forge|forge|"                             # the four-tab forge with the materials wallet
  "mastery|buildtree|RH_SHOT_FRONTIER=1"     # the mastery tree at the frontier: taken nodes and their names
  "map|corrupted|"                           # six regions conquered and the corruption ladder
  "traits|dust|RH_SHOT_AWAKENED=18"          # eighteen traits awakened, three worn
  "gear|character|"                          # the champion's gear
  "roster|roster|"                           # ten champions in five classes
  "warren|warrenready|"                      # the camp that works while you are away
  "title|title|"                             # the title screen (last: the logo is on the capsule)
)

mkdir -p "$DEST" build/shots/polish
# A failed capture leaves no file — so an older run's file must not be there to be copied in its place.
find build/shots/polish -maxdepth 1 -name 'store_*' -delete
specs=()
for row in "${SHOTS[@]}"; do
  IFS='|' read -r name mode extra <<<"$row"
  dials="$SHOW"; [ -n "$extra" ] && dials="$SHOW;$extra"
  specs+=("$mode:100:$dials")
done
bash tools/asset-pipeline/polish_shots.sh store "${specs[@]}" | grep -v '^shots done' || true

fail=0
for row in "${SHOTS[@]}"; do
  IFS='|' read -r name mode extra <<<"$row"
  label="$mode"
  dials="$SHOW"; [ -n "$extra" ] && dials="$SHOW;$extra"
  label="${mode}_$(echo "$dials" | tr 'A-Z' 'a-z' | sed 's/rh_shot_//g' | tr ';,=' '___' | tr -s '_' | sed 's/^_//;s/_$//')"
  src="build/shots/polish/store_${label}_100.png"
  if [ -f "$src" ]; then cp "$src" "$DEST/$name.png"; echo "ok   $DEST/$name.png"; else echo "FAIL $name ($src)"; fail=1; fi
done
exit $fail
