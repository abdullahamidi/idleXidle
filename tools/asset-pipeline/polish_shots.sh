#!/bin/bash
# Verification captures for the release polish pass — a named list, at chosen scales.
#
#   bash tools/asset-pipeline/polish_shots.sh <tag> "mode[:scale[:ENV=V;ENV=V]]" ...
#
# Dials are separated by ';' (a dial's own value may hold commas — RH_SHOT_SWORN=a,b).
#
# Writes build/shots/polish/<tag>_<mode>_<scale>.png. Builds once; one game run per capture.
set -u
. "$(dirname "${BASH_SOURCE[0]}")/../shellenv.sh" || exit 1
cd "$(dirname "${BASH_SOURCE[0]}")/../.." || exit 1
SHOT_LOG="build/shots/polish/.last_run.log"; export SHOT_LOG
TAG="${1:?tag}"; shift
OUTDIR="build/shots/polish"
mkdir -p "$OUTDIR"
WINDIR="$(winpath "$PWD")"

dn build src/IdleXIdle.Game -v q --nologo >/dev/null 2>&1 || { echo "build failed" >&2; exit 1; }

for spec in "$@"; do
  IFS=':' read -r mode sc extra <<<"$spec"
  sc="${sc:-100}"
  label="$mode"
  # The label keeps the dial's name (lower-cased, minus the RH_SHOT_ prefix) so two dials with the same
  # value do not overwrite each other: pose_vow_complete vs sworn_vow_complete.
  [ -n "${extra:-}" ] && label="${mode}_$(echo "$extra" | tr 'A-Z' 'a-z' | sed 's/rh_shot_//g' | tr ';,=' '___' | tr -s '_' | sed 's/^_//;s/_$//')"
  out="$OUTDIR/${TAG}_${label}_${sc}.png"
  RH_ENV=(RH_SHOT="$WINDIR\\${out//\//\\}" RH_SHOT_MODE="$mode" RH_SHOT_UISCALE="$sc")
  if [ -n "${extra:-}" ]; then IFS=';' read -ra kvs <<<"$extra"; for kv in "${kvs[@]}"; do RH_ENV+=("$kv"); done; fi
  timeout 90 bash -c '
    . tools/shellenv.sh || exit 1
    RH_ENV=("$@")
    dn run --project src/IdleXIdle.Game --no-build >"$SHOT_LOG" 2>&1
  ' _ "${RH_ENV[@]}"
  # A capture that produced no file FAILED — and says why: a posed cursor outside its space, or a
  # dial that does not parse, throws in the game (Game1.ParsePosedCursor) rather than photographing
  # a plausible default, and the last lines of its log are the message.
  # The log is UTF-8 text (Program.cs sets the console's encoding), so an ordinary grep reads it.
  if [ -f "$out" ]; then echo "ok   $out"; else echo "FAIL $out"; grep -E 'Exception|RH_SHOT' "$SHOT_LOG" | tail -3 | sed 's/^/     /'; fi
done
echo "shots done"
