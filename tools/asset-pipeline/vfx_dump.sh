#!/bin/bash
# Capture a shot AND the VFX contract's own numbers, which capture.sh throws away.
#
#   bash tools/asset-pipeline/vfx_dump.sh <mode> <outfile.png> [more RH_* already exported]
#
# capture.sh sends the game's stdout to /dev/null, which is right for a screenshot and wrong for
# the two diagnostics the VFX placement contract produces:
#
#   RH_VFX_DUMP=1     one line per effect the first time it resolves — id, subject, anchor, the
#                     resolved frame and content rectangles, and the native-scale ratio with its
#                     verdict. This is the artifact that says whether a placement is RIGHT; the
#                     screenshot only says whether it looks plausible.
#   RH_VFX_BUDGET=1   every profile against every strip it can wear, on the hunter, on the smallest
#                     creature the arena makes, on a boss and on a row. The §73 / LAW 16 ledger,
#                     measured from the real textures rather than from constants typed into a test.
#
# Both are written beside the shot as <outfile>.vfx.txt.
#
#   RH_VFX_DUMP=1 RH_SHOT_SHIELDFX=hold RH_SHOT_HUNTER=quiver \
#     bash tools/asset-pipeline/vfx_dump.sh vfxdebug build/shots/quiver.png
set -u
. "$(dirname "${BASH_SOURCE[0]}")/../shellenv.sh" || exit 1
cd "$(dirname "${BASH_SOURCE[0]}")/../.." || exit 1

MODE="${1:-vfxdebug}"
OUT="${2:-shot_$MODE.png}"
LOG="${OUT%.png}.vfx.txt"
mkdir -p "$(dirname "$OUT")"
rm -f "$OUT" "$LOG"

WINDIR="$(winpath "$PWD")"
dn build src/IdleXIdle.Game -v q --nologo >/dev/null 2>&1 || { echo "build failed" >&2; exit 1; }

RH_ENV=(RH_SHOT="$WINDIR\\$OUT" RH_SHOT_MODE="$MODE")
[ -n "${RH_SHOT_UISCALE:-}" ] && RH_ENV+=(RH_SHOT_UISCALE="$RH_SHOT_UISCALE")
[ -n "${RH_SHOT_T:-}" ] && RH_ENV+=(RH_SHOT_T="$RH_SHOT_T")
dn run --project src/IdleXIdle.Game --no-build 2>/dev/null | grep -E '^vfx' > "$LOG"

[ -f "$OUT" ] && echo "captured $OUT ($(stat -c%s "$OUT") bytes), $(wc -l < "$LOG") vfx line(s) in $LOG" \
  || { echo "capture failed" >&2; exit 1; }
