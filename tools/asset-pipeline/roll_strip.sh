#!/bin/bash
# Re-roll an animation strip until it passes the quality gate, keeping the winner.
#
#   bash tools/asset-pipeline/roll_strip.sh <key> [attempts]
#
# Same argument as roll_rig_source.sh, for the same reason: the variance is in the model,
# not in the wording. Across the first twenty character clips, twelve passed and eight did
# not, with no pattern in the prompts — the same action text produced a clean clip for one
# character and seventeen floating blobs for another.
#
# measure_strip.py judges the result without a human looking at it (framing against the
# character's own base sprite, scale drift, baseline drift, disconnected blobs), so rolling
# is safe: nothing unmeasured is ever kept. The current file is backed up first and restored
# if every attempt fails.
set -uo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")/../.." || exit 1

KEY="${1:?usage: roll_strip.sh <key> [attempts]}"
ATTEMPTS="${2:-4}"

FILE="$(find assets/art/Animations -name "$KEY.png" | head -1)"
BACKUP=""
if [ -n "$FILE" ]; then
  BACKUP="$(mktemp)"
  cp "$FILE" "$BACKUP"
  if python3 tools/asset-pipeline/measure_strip.py "$KEY" >/dev/null 2>&1; then
    echo "$KEY already passes the gate."
    rm -f "$BACKUP"; exit 0
  fi
fi

for i in $(seq 1 "$ATTEMPTS"); do
  echo "── $KEY: attempt $i/$ATTEMPTS"
  python3 tools/asset-pipeline/animate.py --force --only "$KEY" >/dev/null 2>&1
  OUT="$(python3 tools/asset-pipeline/measure_strip.py "$KEY" 2>&1)"
  RC=$?
  echo "$OUT" | sed 's/^/   /'
  if [ $RC -eq 0 ]; then
    echo "   PASS on attempt $i"
    rm -f "$BACKUP"; exit 0
  fi
done

# Every attempt failed. DELETE the strip rather than restore it.
#
# Restoring would put a clip the gate rejected back where the game will happily draw it —
# AnimSprite plays whatever is on disk and knows nothing about the gate. The draw sites fall
# back clip -> idle -> base sprite, so deleting means the character stands there as their
# approved design instead of pulsing, sliding, or dragging seventeen loose blobs across the
# arena. No clip is better than a broken clip, and a rejected file left on disk is a rejection
# that does not take effect.
echo "$KEY: $ATTEMPTS attempts, none passed — removing it so the draw path falls back." >&2
[ -n "$FILE" ] && rm -f "$FILE"
rm -f "$BACKUP"
exit 1
