#!/usr/bin/env bash
# The prologue, photographed: every authored beat at 100 / 125 / 150, plus Reduced Motion at 100.
#
#   bash tools/asset-pipeline/prologue_fixtures.sh [filter]
#
# Rows are <state>|<scale>|<dials>; files land in production/qa/evidence/visual-pass/prologue/
# as <state>_<scale>[_reduced].png. RH_SHOT_OPENING=Prologue pins the stage and RH_SHOT_BEAT the
# beat (the posed rig holds the clock at 1 s, so the pan is filmed by the live opening flow, not
# here). Any mode works under the pose; `fight` is the cheapest to dress.
set -u
. "$(dirname "${BASH_SOURCE[0]}")/../shellenv.sh" || exit 1
cd "$(dirname "${BASH_SOURCE[0]}")/../.." || exit 1
WINDIR="$(winpath "$PWD")"
FILTER="${1:-}"
OUTDIR="production/qa/evidence/visual-pass/prologue"
mkdir -p "$OUTDIR"
SHOT_LOG="build/shots/.prologue_fixtures.log"
export SHOT_LOG
mkdir -p build/shots
dn build src/IdleXIdle.Game -v q --nologo >/dev/null 2>&1 || { echo "build failed" >&2; exit 1; }

ROWS=()
for beat in 0 1 2 3 4 5; do
  for sc in 100 125 150; do
    ROWS+=("beat${beat}|${sc}|RH_SHOT_OPENING=Prologue;RH_SHOT_BEAT=${beat}")
  done
  ROWS+=("beat${beat}|100|RH_SHOT_OPENING=Prologue;RH_SHOT_BEAT=${beat};RH_SHOT_REDUCED=1")
done

taken=0; failed=0
for row in "${ROWS[@]}"; do
  IFS='|' read -r state sc extra <<<"$row"
  [ -n "$FILTER" ] && [[ "$state" != *"$FILTER"* ]] && continue
  suffix=""; [[ "$extra" == *"RH_SHOT_REDUCED=1"* ]] && suffix="_reduced"
  out="$OUTDIR/${state}_${sc}${suffix}.png"
  RH_ENV=(RH_SHOT="$WINDIR\\${out//\//\\}" RH_SHOT_MODE="fight" RH_SHOT_UISCALE="$sc")
  IFS=';' read -ra kvs <<<"$extra"; for kv in "${kvs[@]}"; do RH_ENV+=("$kv"); done
  rm -f "$out"
  timeout 120 bash -c '
    . tools/shellenv.sh || exit 1
    RH_ENV=("$@")
    dn run --project src/IdleXIdle.Game --no-build >"$SHOT_LOG" 2>&1
  ' _ "${RH_ENV[@]}"
  if [ -f "$out" ]; then echo "ok   $out"; taken=$((taken+1));
  else echo "FAIL $out"; failed=$((failed+1)); grep -E 'Exception|RH_SHOT' "$SHOT_LOG" | tail -3 | sed 's/^/     /'; fi
done
echo "captured $taken, failed $failed"
exit $failed
