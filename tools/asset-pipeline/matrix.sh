#!/usr/bin/env bash
# The UI SCALE x window matrix (UI polish brief §109-§110, §102-§107).
#
#   bash tools/asset-pipeline/matrix.sh [outdir]            (default build/shots/matrix)
#   MODES="weave character" SCALES="100 150" WINDOWS="1280x720" bash tools/asset-pipeline/matrix.sh
#
# Six screens (BUILD GEAR FORGE TRAITS SETTINGS VAULT) at every density profile (100 / 125 / 150) and
# every window the brief names (1280x720, 1600x900, 1920x1080) — 54 captures, each the PRESENTED frame
# at the real window size (RH_SHOT_WINDOW), so what is photographed is what a player at that window
# sees, letterbox and all. Every file is meant to be LOOKED at: a capture nobody opened proves nothing.
#
# The settings capture doubles as the Settings escape test (§83-§85): at 150 % in a 1280x720 window the
# UI SCALE row and its buttons must be visible and inside the window, or a player who picked 150 % on a
# small screen has no way back.
set -u
cd "$(dirname "$0")/../.."
OUT="${1:-build/shots/matrix}"
mkdir -p "$OUT"
MODES="${MODES:-weave character forge dust settings vault}"
SCALES="${SCALES:-100 125 150}"
WINDOWS="${WINDOWS:-1280x720 1600x900 1920x1080}"
fails=0
for mode in $MODES; do
  for scale in $SCALES; do
    for win in $WINDOWS; do
      f="$OUT/${mode}_${scale}_${win}.png"
      if RH_SHOT_UISCALE="$scale" RH_SHOT_WINDOW="$win" bash tools/asset-pipeline/capture.sh "$mode" "$f" >/dev/null 2>&1 && [ -s "$f" ]; then
        echo "ok   $f"
      else
        echo "FAIL $f"; fails=$((fails+1))
      fi
    done
  done
done
echo "matrix: $(ls "$OUT"/*.png 2>/dev/null | wc -l) captures in $OUT, $fails failed"
[ "$fails" -eq 0 ]
