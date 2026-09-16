#!/usr/bin/env bash
# The item cell, photographed: Gear (selected and hovered showcase), Forge, the chest reveal and the
# trader, at 100 / 125 / 150.
#
#   bash tools/asset-pipeline/item_fixtures.sh [filter]
#
# Rows are <state>|<mode>|<dials>|<third arg>; files land in production/qa/evidence/visual-pass/items/
# as <state>_<scale>.png. The showcase hover coordinates are PAGE space (see capture.sh), one per
# density, because the grid reflows.
set -u
. "$(dirname "${BASH_SOURCE[0]}")/../shellenv.sh" || exit 1
cd "$(dirname "${BASH_SOURCE[0]}")/../.." || exit 1
WINDIR="$(winpath "$PWD")"
FILTER="${1:-}"
OUTDIR="production/qa/evidence/visual-pass/items"
mkdir -p "$OUTDIR" build/shots
SHOT_LOG="build/shots/.item_fixtures.log"
export SHOT_LOG
dn build src/IdleXIdle.Game -v q --nologo >/dev/null 2>&1 || { echo "build failed" >&2; exit 1; }

declare -A HOVER=([100]="893,458" [125]="858,567" [150]="918,662")
ROWS=()
for sc in 100 125 150; do
  ROWS+=("gear_selected|$sc|character|RH_SHOT_GEAR_POSE=showcase|")
  ROWS+=("gear_showcase|$sc|character|RH_SHOT_GEAR_POSE=showcase;RH_SHOT_PAGE_MOUSE=${HOVER[$sc]}|")
  ROWS+=("forge|$sc|forge||")
  ROWS+=("reveal|$sc|lootforge||1.2")
  ROWS+=("trader|$sc|trader||")
done

taken=0; failed=0
for row in "${ROWS[@]}"; do
  IFS='|' read -r state sc mode extra third <<<"$row"
  [ -n "$FILTER" ] && [[ "${state}_${sc}" != *"$FILTER"* ]] && continue
  out="$OUTDIR/${state}_${sc}.png"
  RH_ENV=(RH_SHOT="$WINDIR\\${out//\//\\}" RH_SHOT_MODE="$mode" RH_SHOT_UISCALE="$sc")
  if [ -n "$extra" ]; then IFS=';' read -ra kvs <<<"$extra"; for kv in "${kvs[@]}"; do RH_ENV+=("$kv"); done; fi
  [ -n "$third" ] && RH_ENV+=(RH_SHOT_ZOOM="$third" RH_SHOT_T="$third")
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
