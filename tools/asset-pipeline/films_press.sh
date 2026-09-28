#!/usr/bin/env bash
# tools/asset-pipeline/films_press.sh <tag> [takes...] -- PRESS, the FIELD / AURA reference (ADR-011, 2026-09-28): the Seeker's pressure field,
# its compression, the pressure front and the crush, filmed from the seeded fight (the same build as the JAWS films).
#   <tag>/normal       normal TEMPO, every frame around the PRESS tick at 6000 (the front enemy crushed)
#   <tag>/repeated     normal TEMPO, one picture per 2 frames, 5.5-10.5 s: three ticks, the target moving as creatures fall
#   <tag>/fast         fast TEMPO (the second segment), every frame 0.8-4.8 s: SPRAY at 1400 then the tick at 2000; HARD
#                      HANDS at 3600 then the tick at 4000 (the overlaps)
#   <tag>/normal_off   `normal` with RH_FIELD_RECIPES=0: the generic held aura PRESS had before (the comparison)
#   <tag>/yield        normal TEMPO, every frame around the tick at 10000, where JAWS bites the same creature (PRESS gives way)
cd "$(dirname "${BASH_SOURCE[0]}")/../.." || exit 1
TAG=${1:-current}; shift
OUT=build/shots/press/$TAG
mkdir -p "$OUT"
SWAP_BODY="volley_spray:volley_spray@Body"
TEMPO="bite,swift,quick,road_sign_2,brisk,rhythm,blitz,volley"
film() {  # name T count stride start [ENV=VAL ...]
  local name=$1 t=$2 count=$3 stride=$4 start=$5; shift 5
  env RH_SHOT_SWAP="$SWAP_BODY" RH_SHOT_SEED=7 "$@" RH_PRESENT_TRACE=1 RH_SHOT_HUNTER=seeker RH_SHOT_T="$t" \
    RH_SEQ_LOG="$PWD/$OUT/$name.log" timeout 2400 bash tools/asset-pipeline/capture_seq.sh fight "$count" "$stride" "$OUT/$name" "$start" 2>&1 | tail -1
}
want() { [ ${#ARGS[@]} -eq 0 ] || [[ " ${ARGS[*]} " == *" $1 "* ]]; }
ARGS=("$@")
want normal      && film normal 3.4 72 1 53
want repeated    && film repeated 3.4 150 2 60
want fast        && film fast 3.4 240 1 732 RH_SHOT_TAKE="$TEMPO"
want normal_off  && film normal_off 3.4 72 1 53 RH_FIELD_RECIPES=0
want yield       && film yield 3.4 78 1 294
exit 0
