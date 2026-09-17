#!/bin/bash
# THE OPENING, PLAYED FROM A FRESH SAVE BY A HAND THAT IS NOT THERE — and read back for its ORDER.
#
#   bash tools/check_opening_flow.sh [uiscale=100] [dwell-frames=120] [outdir]
#
# A brand-new career in an isolated save directory, driven through the whole authored opening by
# Game1.OpeningRig.cs: a synthetic mouse on the REAL input path clicks BEGIN THE HUNT, NEXT, every
# CONTINUE, the lit rail tile, the lit chest, the lit item and EQUIP, after <dwell> frames of reading
# time each. It writes a frame-stamped trace and a film of the moments that matter, and then
# tools/check_opening_trace.py fails the run on any of the orders the 2026-09-11 playtest pass fixed:
#
#   GLEAM      final EnemyDown < last fall finished <= GLEAM card < next wave; next wave after CONTINUE
#   SIGNATURE  hold <= card < CONTINUE < the SAME cast crosses, once; nothing drawn over it until played
#   GEAR       the click on the gift's cell is the pick; ITEM STATS after it; EQUIP wears it; no stall
#
# The run plays at real speed (a full opening is two to three minutes) and never touches the player's
# save: RH_SAVE_DIR points the whole save system at a scratch directory, as the boot check's lanes do.
. "$(dirname "${BASH_SOURCE[0]}")/shellenv.sh" || exit 1
cd "$(dirname "${BASH_SOURCE[0]}")/.." || exit 1

SCALE="${1:-100}"
DWELL="${2:-120}"
OUT="${3:-build/shots/opening_flow/$SCALE}"
SAVEDIR="${TEMP:-/tmp}/rh_opening_flow_$SCALE"

mkdir -p "$OUT" "$SAVEDIR"
rm -f "$OUT"/*.png "$OUT/trace.txt" "$OUT/run.log"
rm -f "$SAVEDIR/save.json" "$SAVEDIR/save.bak" "$SAVEDIR"/save.pre-v*.json

dn build src/IdleXIdle.Game -v q --nologo >/dev/null 2>&1 || { echo "build failed" >&2; exit 1; }

# `dn` is a shell FUNCTION (shellenv.sh), which `timeout` cannot exec — so the run goes through a
# child bash that sources the helpers itself, the way check_boot.sh's lanes do.
timeout 900 bash -c '
  . tools/shellenv.sh || exit 1
  RH_ENV=(RH_OPENING_AUTOPLAY="$1" RH_OPENING_TRACE="$2" RH_OPENING_FILM="$3" RH_SAVE_DIR="$4" RH_SHOT_UISCALE="$5")
  dn run --project src/IdleXIdle.Game --no-build
' _ "$DWELL" "$(winpath "$OUT/trace.txt")" "$(winpath "$OUT")" "$(winpath "$SAVEDIR")" "$SCALE" > "$OUT/run.log" 2>&1
rc=$?

if [ ! -s "$OUT/trace.txt" ]; then
  echo "NO TRACE — the opening rig wrote nothing (exit $rc)." >&2
  tail -20 "$OUT/run.log" >&2
  exit 1
fi
echo "ui scale $SCALE: $(ls "$OUT"/*.png 2>/dev/null | wc -l) frames filmed, exit $rc"
py tools/check_opening_trace.py "$OUT/trace.txt" || exit 1
[ $rc -eq 0 ] || { echo "the run exited $rc" >&2; tail -20 "$OUT/run.log" >&2; exit 1; }
echo "the opening plays from a fresh save, in order, at $SCALE %."
