#!/bin/bash
# Capture a FILMSTRIP of the game, headless: N frames, every S frames, from one posed mode.
#
#   bash tools/asset-pipeline/capture_seq.sh [mode] [count] [stride] [outprefix]
#   bash tools/asset-pipeline/capture_seq.sh fight 24 6 seq_fight     # 24 frames, 100 ms apart
#
# capture.sh proves a layout with one frame; this proves a RHYTHM — whether the swing lands on the
# hit, how long a burst lingers, whether the cast opens as its effect appears. The game saves
# <outprefix>_NN.png at each step (RH_SHOT_SEQ, see Game1.Draw) and
# tools/asset-pipeline/v2/filmstrip.py lays them out as one sheet cropped to the arena.
MODE="${1:-fight}"
COUNT="${2:-24}"
STRIDE="${3:-6}"
OUT="${4:-seq_$MODE}"
. "$(dirname "${BASH_SOURCE[0]}")/../shellenv.sh" || exit 1
cd "$(dirname "${BASH_SOURCE[0]}")/../.." || exit 1
WINDIR="$(winpath "$PWD")"
dn build src/ResonanceHunter.Game -v q --nologo >/dev/null 2>&1 \
  || { echo "build failed" >&2; exit 1; }
rm -f "${OUT}"_[0-9][0-9].png
RH_ENV=(RH_SHOT="$WINDIR\\$OUT.png" RH_SHOT_MODE="$MODE" RH_SHOT_SEQ="$COUNT,$STRIDE")
dn run --project src/ResonanceHunter.Game --no-build >/dev/null 2>&1   # dn applies RH_ENV (shellenv.sh)
N="$(ls "${OUT}"_[0-9][0-9].png 2>/dev/null | wc -l)"
[ "$N" -gt 0 ] || { echo "capture failed" >&2; exit 1; }
py tools/asset-pipeline/v2/filmstrip.py "$OUT" "${OUT}"_[0-9][0-9].png
echo "captured $N frames -> ${OUT}_strip.png"
