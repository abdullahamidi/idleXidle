#!/bin/bash
# Full library regeneration, start to finish, unattended.
#
# Runs the stages SEQUENTIALLY on purpose. The API allows 8 concurrent jobs and
# an animation holds a slot for ~85s, so running stills and animations at the
# same time just makes both sides collide, back off, and crawl.
#
# Every stage is resumable: generate.py and animate.py skip whatever is already
# on disk unless --replace/--force is passed, so re-running after a crash picks
# up where it stopped.
#
# Usage: bash tools/asset-pipeline/run_all.sh [--replace]

set -uo pipefail
. "$(dirname "${BASH_SOURCE[0]}")/../shellenv.sh" || exit 1
cd "$(dirname "${BASH_SOURCE[0]}")/../.." || exit 1

REPLACE="${1:-}"
# animate.py spells it --force; generate.py spells it --replace
REPLACE_ANIM=""
[ "$REPLACE" = "--replace" ] && REPLACE_ANIM="--force"
LOG_DIR="tools/asset-pipeline/.staging"
mkdir -p "$LOG_DIR"

stamp() { date -u +%H:%M:%S; }

echo "[$(stamp)] === STAGE 1/4: stills ==="
py -u tools/asset-pipeline/generate.py $REPLACE 2>&1 | tail -60
echo "[$(stamp)] stills rc=${PIPESTATUS[0]}"

echo
echo "[$(stamp)] === STAGE 2/4: animation + VFX strips ==="
py -u tools/asset-pipeline/animate.py --workers 3 $REPLACE_ANIM 2>&1 | tail -60
echo "[$(stamp)] animate rc=${PIPESTATUS[0]}"

echo
echo "[$(stamp)] === STAGE 3/4: derive statics from strips ==="
py -u tools/asset-pipeline/derive_statics.py 2>&1 | tail -30

echo
echo "[$(stamp)] === STAGE 4/4: audit ==="
py -u tools/asset-pipeline/audit.py 2>&1 | head -30

echo
echo "[$(stamp)] === DONE ==="
