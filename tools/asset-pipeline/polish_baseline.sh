#!/bin/bash
# Baseline captures for the release polish pass: every screen at the three density profiles.
#
#   bash tools/asset-pipeline/polish_baseline.sh [outdir] [tag]
#
# Writes build/shots/polish/<tag>_<mode>_<scale>.png. Builds once; each capture is one game run.
set -u
. "$(dirname "${BASH_SOURCE[0]}")/../shellenv.sh" || exit 1
cd "$(dirname "${BASH_SOURCE[0]}")/../.." || exit 1
OUTDIR="${1:-build/shots/polish}"
TAG="${2:-base}"
mkdir -p "$OUTDIR"
WINDIR="$(winpath "$PWD")"

dn build src/IdleXIdle.Game -v q --nologo >/dev/null 2>&1 || { echo "build failed" >&2; exit 1; }

# mode|extra env (space separated NAME=VALUE)|label
SHOTS=(
  "fight||hunt"
  "boss||boss"
  "character||gear"
  "stats||training"
  "weave||build"
  "vow||vow"
  "buildtree||mastery"
  "vault||vault"
  "forge||forge"
  "warren||warren"
  "map||map"
  "dust||traits"
  "roster||roster"
  "settings||settings"
  "help||help"
  "welcome||welcome"
  "dispatches|RH_SHOT_DISPATCH=longest|dispatch"
  "tour|RH_SHOT_TAB=Mastery RH_SHOT_STEP=3|tourmastery3"
  "tour|RH_SHOT_TAB=Mastery RH_SHOT_STEP=1|tourmastery1"
  "tour|RH_SHOT_TAB=Build RH_SHOT_STEP=3|tourbuild3"
  "tour|RH_SHOT_TAB=Gear RH_SHOT_STEP=1|tourgear1"
  "intro||intro1"
)
SCALES=(100 125 150)

for entry in "${SHOTS[@]}"; do
  IFS='|' read -r mode extra label <<<"$entry"
  for sc in "${SCALES[@]}"; do
    out="$OUTDIR/${TAG}_${label}_${sc}.png"
    RH_ENV=(RH_SHOT="$WINDIR\\${out//\//\\}" RH_SHOT_MODE="$mode" RH_SHOT_UISCALE="$sc")
    for kv in $extra; do RH_ENV+=("$kv"); done
    timeout 90 bash -c '
      . tools/shellenv.sh || exit 1
      RH_ENV=("$@")
      dn run --project src/IdleXIdle.Game --no-build >/dev/null 2>&1
    ' _ "${RH_ENV[@]}"
    if [ -f "$out" ]; then echo "ok   $out"; else echo "FAIL $out"; fi
  done
done
echo "baseline done"
