#!/bin/bash
# Claude Code PostToolUse hook: generate art for newly-added content.
#
# Fires when a file that can introduce new asset keys is written, then asks
# audit.py whether anything the game references is still missing. If so, and a
# PIXELLAB_API_TOKEN is available, it launches generate.py DETACHED — generation
# takes minutes and a hook that blocked on it would time out.
#
# Never blocks and never fails the tool call: exit 0 in every path. The worst
# case is an advisory line on stderr.

INPUT=$(cat)

REPO="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
PIPELINE="$REPO/tools/asset-pipeline"
STAGING="$PIPELINE/.staging"
LOCK="$STAGING/.generating"
LOG="$STAGING/generate.log"

# Parse the written path -- jq if available, else a grep fallback.
if command -v jq >/dev/null 2>&1; then
    FILE_PATH=$(echo "$INPUT" | jq -r '.tool_input.file_path // empty')
else
    FILE_PATH=$(echo "$INPUT" | grep -oE '"file_path"[[:space:]]*:[[:space:]]*"[^"]*"' | sed 's/.*:[[:space:]]*"//;s/"$//')
fi
FILE_PATH=${FILE_PATH//\\//}
[ -z "$FILE_PATH" ] && exit 0

# Only these can introduce a new asset key: the manifest itself, client code
# that names keys, and the design docs that define new content.
case "$FILE_PATH" in
    *tools/asset-pipeline/manifest.json) ;;
    *src/ResonanceHunter.Game/*.cs) ;;
    *design/gdd/*.md|*design/art/*.md) ;;
    *) exit 0 ;;
esac

PYTHON_CMD=""
for cmd in python3 python py; do
    command -v "$cmd" >/dev/null 2>&1 && { PYTHON_CMD="$cmd"; break; }
done
[ -z "$PYTHON_CMD" ] && exit 0
[ -f "$PIPELINE/generate.py" ] || exit 0

# Anything in the manifest that has no PNG yet.
PENDING=$("$PYTHON_CMD" "$PIPELINE/generate.py" --dry-run 2>/dev/null | grep -c '^  ' || true)
[ "${PENDING:-0}" -eq 0 ] && exit 0

if [ -z "$PIXELLAB_API_TOKEN" ]; then
    echo "[assets] $PENDING manifest asset(s) missing. Set PIXELLAB_API_TOKEN and run:" >&2
    echo "         python3 tools/asset-pipeline/generate.py" >&2
    exit 0
fi

mkdir -p "$STAGING"

# A generation run already in flight -- do not stack a second one. The lock is
# stale-checked so a killed run cannot wedge the hook permanently.
if [ -f "$LOCK" ]; then
    LOCK_PID=$(cat "$LOCK" 2>/dev/null)
    if [ -n "$LOCK_PID" ] && kill -0 "$LOCK_PID" 2>/dev/null; then
        exit 0
    fi
    rm -f "$LOCK"
fi

(
    echo $$ > "$LOCK"
    trap 'rm -f "$LOCK"' EXIT
    {
        echo "=== $(date -u +%Y-%m-%dT%H:%M:%SZ) triggered by $FILE_PATH ==="
        "$PYTHON_CMD" "$PIPELINE/generate.py"
    } >> "$LOG" 2>&1
) </dev/null >/dev/null 2>&1 &
disown 2>/dev/null

echo "[assets] generating $PENDING missing asset(s) in the background -> tools/asset-pipeline/.staging/generate.log" >&2
exit 0
