#!/bin/bash
# JAWS CONCEPT STUDY (2026-09-25), THROWAWAY: film the CLEAN PLATES the concept animatics are drawn over.
#
# The seeded fight (RH_SHOT_SEED=7) with the reaction layer OFF (RH_REACTION_RECIPES=0): the fight is identical with the
# layer on or off; off, the reflected number and flash sit on the bite's own frame and no JAWS trap, chain or yank is
# drawn. The legacy row ring (fx_seeker_trap_strip8_512, and its fallback fx_trap_strip8_512) is moved out of the BUILD
# OUTPUT for the takes and put back on exit (never a repo file), so the game runs --no-build (a build would copy it back).
#
#   plate_normal    normal TEMPO, every frame round the bite at 7000 (JAWS answers the front imp)
#   plate_spray     fast TEMPO, JAWS at 1000 (wave 2) in SPRAY's wind-up, the knives out at 1150
#   plate_hh        fast TEMPO, JAWS at 13000 (wave 1), HARD HANDS leaps at 13083
#   plate_rep_a/b/c fast TEMPO, three consecutive 99-frame takes (wave 2: JAWS at 1000, 3000, 5000)
#
#   bash prototypes/jaws-concepts/film_plates.sh [plate ...]      (build the game once first: dotnet build)
. "$(dirname "${BASH_SOURCE[0]}")/../../tools/shellenv.sh" || exit 1
cd "$(dirname "${BASH_SOURCE[0]}")/../.." || exit 1
OUT="${PLATES_OUT:-build/shots/jaws/plates}"      # re-filming shifts frame<->playhead (startup jitter): re-render after
mkdir -p "$OUT"
VFX=src/IdleXIdle.Game/bin/Debug/net8.0/assets/art/VFX
HOLD="$(mktemp -d)"
for f in seeker_trap/fx_seeker_trap_strip8_512.png trap/fx_trap_strip8_512.png; do
  [ -f "$VFX/$f" ] && mv "$VFX/$f" "$HOLD/$(basename "$f")"
done
restore() {
  [ -f "$HOLD/fx_seeker_trap_strip8_512.png" ] && mv "$HOLD/fx_seeker_trap_strip8_512.png" "$VFX/seeker_trap/"
  [ -f "$HOLD/fx_trap_strip8_512.png" ] && mv "$HOLD/fx_trap_strip8_512.png" "$VFX/trap/"
  rmdir "$HOLD" 2>/dev/null
}
trap restore EXIT
WINDIR="$(winpath "$PWD")"
TEMPO="bite,swift,quick,road_sign_2,brisk,rhythm,blitz,volley"
film() {  # name T count start [ENV=VAL ...]   (the capture exits on its own after its sequence)
  local name=$1 t=$2 count=$3 start=$4; shift 4
  rm -f "$OUT/${name}"_[0-9][0-9].png
  RH_ENV=(RH_SHOT_SWAP="volley_spray:volley_spray@Body" RH_SHOT_SEED=7 RH_REACTION_RECIPES=0 "$@" RH_PRESENT_TRACE=1
          RH_SHOT_HUNTER=seeker RH_SHOT_T="$t" RH_SHOT="$WINDIR\\${OUT//\//\\}\\$name.png" RH_SHOT_MODE=fight
          RH_SHOT_SEQ="$count,1,$start")
  dn run --project src/IdleXIdle.Game --no-build >"$OUT/$name.log" 2>&1
  echo "$name: $(ls "$OUT/${name}"_[0-9][0-9].png 2>/dev/null | wc -l) frames"
}
want() { [ ${#ARGS[@]} -eq 0 ] || [[ " ${ARGS[*]} " == *" $1 "* ]]; }
ARGS=("$@")
want plate_normal && film plate_normal 3.4 99 115
want plate_spray  && film plate_spray 3.4 60 725 RH_SHOT_TAKE="$TEMPO"
want plate_hh     && film plate_hh 3.4 60 455 RH_SHOT_TAKE="$TEMPO"
want plate_rep_a  && film plate_rep_a 3.4 99 720 RH_SHOT_TAKE="$TEMPO"
want plate_rep_b  && film plate_rep_b 3.4 99 812 RH_SHOT_TAKE="$TEMPO"
want plate_rep_c  && film plate_rep_c 3.4 99 918 RH_SHOT_TAKE="$TEMPO"
exit 0
