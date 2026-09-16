#!/usr/bin/env bash
# The enemy matrix, photographed in the live arena.
#
#   bash tools/asset-pipeline/enemy_fixtures.sh [filter]
#
# Rows are <name>|<scale>|<dials>; files land in production/qa/evidence/visual-pass/enemies/ as
# <name>_<scale>.png (with <name>_<scale>.png.events.txt / .actors.txt beside a DUMP row).
#
#   cell_<region>_<role>   every one of the 24 cells at 100 %: RH_SHOT_SOURCE picks the family (the
#                          region whose theme that Source is), RH_SHOT_ARCHETYPE the body and its box.
#   <family>_pack          a Swarm pack           } Verdant Hollow, Cinderworks and The Pale Choir (the
#   <family>_lone          a lone Bruiser         } starting family, the second, and the last), each at
#   <family>_cast          a Caster mid-attack    } 100 / 125 / 150. `cast` lands the shutter just before
#   <family>_armoured      an Armoured pair       } a bite (RH_SHOT_BITE) so the attack clip is on screen.
#   inspect_<family>       the inspector over a creature: its own name, role and Source glyph.
#
# The posed rig restarts the fight on its first live frame, so a seek is what puts creatures on the
# page; 0.8 s is past the entrance fade. Everything here is presentation: the dials change which art the
# arena draws and how it lays it out, never a number the fight uses.
set -u
. "$(dirname "${BASH_SOURCE[0]}")/../shellenv.sh" || exit 1
cd "$(dirname "${BASH_SOURCE[0]}")/../.." || exit 1
WINDIR="$(winpath "$PWD")"
FILTER="${1:-}"
OUTDIR="production/qa/evidence/visual-pass/enemies"
mkdir -p "$OUTDIR"
SHOT_LOG="build/shots/.enemy_fixtures.log"
export SHOT_LOG
mkdir -p build/shots
dn build src/IdleXIdle.Game -v q --nologo >/dev/null 2>&1 || { echo "build failed" >&2; exit 1; }

declare -A FAMILY=( [verdant]=Nature [cinder]=Machine [umbral]=Shadow [marrow]=Body [archive]=Mind [choir]=Spirit )
ROWS=()
# A forced role keeps the wave's rolled COUNT unless told otherwise, so every row poses a count the role
# can really roll (Archetypes.Shapes: Swarm 3-5, Caster 1-2, Armoured 1-2, Bruiser exactly 1).
declare -A COUNT=( [Swarm]="" [Caster]="RH_SHOT_CREATURES=2;" [Armoured]="RH_SHOT_CREATURES=2;" [Bruiser]="RH_SHOT_CREATURES=1;" )
for fam in verdant cinder umbral marrow archive choir; do
  for role in Swarm Caster Armoured Bruiser; do
    lower="$(echo "$role" | tr '[:upper:]' '[:lower:]')"
    ROWS+=("cell_${fam}_${lower}|100|fight|${COUNT[$role]}RH_SHOT_SOURCE=${FAMILY[$fam]};RH_SHOT_ARCHETYPE=${role};RH_SHOT_T=0.8")
  done
done
for fam in verdant cinder choir; do
  src="${FAMILY[$fam]}"
  for sc in 100 125 150; do
    ROWS+=("${fam}_pack|${sc}|fight|RH_SHOT_SOURCE=${src};RH_SHOT_ARCHETYPE=Swarm;RH_SHOT_T=0.8")
    ROWS+=("${fam}_lone|${sc}|fight|RH_SHOT_SOURCE=${src};RH_SHOT_ARCHETYPE=Bruiser;RH_SHOT_CREATURES=1;RH_SHOT_T=0.8")
    ROWS+=("${fam}_cast|${sc}|fight|RH_SHOT_CREATURES=2;RH_SHOT_SOURCE=${src};RH_SHOT_ARCHETYPE=Caster;RH_SHOT_BITE=1;RH_SHOT_LEAD=0.12;RH_SHOT_DUMP=1")
    ROWS+=("${fam}_armoured|${sc}|fight|RH_SHOT_CREATURES=2;RH_SHOT_SOURCE=${src};RH_SHOT_ARCHETYPE=Armoured;RH_SHOT_T=0.8")
  done
done
# The inspector over the right-most creature of a Verdant and a Choir pack (canvas-space pointer).
ROWS+=("inspect_verdant|100|fightinspect|RH_SHOT_CREATURES=2;RH_SHOT_SOURCE=Nature;RH_SHOT_ARCHETYPE=Caster;RH_SHOT_T=0.8;RH_SHOT_CANVAS_MOUSE=1560,640")
ROWS+=("inspect_choir|150|fightinspect|RH_SHOT_CREATURES=2;RH_SHOT_SOURCE=Spirit;RH_SHOT_ARCHETYPE=Caster;RH_SHOT_T=0.8;RH_SHOT_CANVAS_MOUSE=1560,640")

taken=0; failed=0
for row in "${ROWS[@]}"; do
  IFS='|' read -r name sc mode extra <<<"$row"
  [ -n "$FILTER" ] && [[ "$name" != *"$FILTER"* ]] && continue
  out="$OUTDIR/${name}_${sc}.png"
  RH_ENV=(RH_SHOT="$WINDIR\\${out//\//\\}" RH_SHOT_MODE="$mode" RH_SHOT_UISCALE="$sc")
  IFS=';' read -ra kvs <<<"$extra"; for kv in "${kvs[@]}"; do RH_ENV+=("$kv"); done
  rm -f "$out"
  # Two tries: a capture whose graphics device could not be created (a display asleep for a moment)
  # is retried once after a pause rather than recorded as a missing picture.
  for try in 1 2; do
    timeout 120 bash -c '
      . tools/shellenv.sh || exit 1
      RH_ENV=("$@")
      dn run --project src/IdleXIdle.Game --no-build >"$SHOT_LOG" 2>&1
    ' _ "${RH_ENV[@]}"
    [ -f "$out" ] && break
    sleep 15
  done
  if [ -f "$out" ]; then echo "ok   $out"; taken=$((taken+1));
  else echo "FAIL $out"; failed=$((failed+1)); grep -E 'Exception|RH_SHOT' "$SHOT_LOG" | tail -3 | sed 's/^/     /'; fi
done
echo "captured $taken, failed $failed"
exit $failed
