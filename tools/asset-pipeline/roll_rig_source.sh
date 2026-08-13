#!/bin/bash
# Re-roll a rig SOURCE figure until it passes the cut gate, keeping the winner.
#
#   bash tools/asset-pipeline/roll_rig_source.sh <key> [attempts]
#
# Roster bodies are a coin flip. The same prompt produced a clean cut-out A-pose on one
# roll and a figure standing in a scene on the next, and four rounds of prompt surgery
# moved which check failed without raising the hit rate — the variance is in the model,
# not in the wording.
#
# That is exactly the situation a GATE is for. measure_rig_source.py reads the armpit
# gaps and the leg gap straight out of the alpha channel and exits non-zero if a
# rectangle decomposition cannot cut the figure, so a roll can be judged without a human
# looking at it. Rolling until it passes is cheaper than arguing with the model, and it
# is safe because nothing unmeasured is ever kept.
#
# The current file is backed up first and restored if every attempt fails, so a bad roll
# can never destroy a good body that was already on disk.
set -uo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")/../.." || exit 1

KEY="${1:?usage: roll_rig_source.sh <key> [attempts]}"
ATTEMPTS="${2:-4}"

FILE="$(find assets/art -name "$KEY.png" | head -1)"
BACKUP=""
if [ -n "$FILE" ]; then
  BACKUP="$(mktemp)"
  cp "$FILE" "$BACKUP"
  if python3 tools/asset-pipeline/measure_rig_source.py "$KEY" >/dev/null 2>&1; then
    echo "$KEY already passes the cut gate — nothing to roll."
    rm -f "$BACKUP"
    exit 0
  fi
fi

for i in $(seq 1 "$ATTEMPTS"); do
  echo "── $KEY: attempt $i/$ATTEMPTS"
  python3 tools/asset-pipeline/generate.py --replace --only "$KEY" >/dev/null 2>&1
  # The measurer's EXIT CODE, not a grep of its output. Under `set -o pipefail` a pipeline
  # takes the last non-zero status, so `measure | grep -q UNCUTTABLE` reported the
  # measurer's own failure exit rather than grep's match and inverted the test — the
  # first roll was announced as a PASS while printing UNCUTTABLE directly underneath it.
  OUT="$(python3 tools/asset-pipeline/measure_rig_source.py "$KEY" 2>&1)"
  RC=$?
  echo "$OUT" | sed 's/^/   /'
  if [ $RC -eq 0 ]; then
    echo "   PASS on attempt $i"
    rm -f "$BACKUP"
    exit 0
  fi
done

echo "$KEY: $ATTEMPTS attempts, none passed the gate." >&2
[ -n "$BACKUP" ] && { cp "$BACKUP" "$FILE"; rm -f "$BACKUP"; echo "restored the previous $KEY" >&2; }
exit 1
