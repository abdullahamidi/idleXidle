#!/bin/bash
# THE BITE FOUNDATION'S REVIEW BATTERY (ADR-012, 2026-09-26): every take the review compares, filmed from one seeded
# fight (RH_SHOT_SEED=7: the Seeker against four GLOOM WHELPS in Umbral Reach; SPRAY woven in Body's red), the reaction
# layer OFF (JAWS is paused: its answer is the reflected number and flash on the bite's own frame) and the legacy row
# ring held out of the BUILD OUTPUT for the takes (the old presentation's, drawn over the pack at every answer).
#
#   normal TEMPO, 99 frames from ~6500 ms: the swing lands at 6700 (150, the front whelp), the pack bites at 7000
#   fast TEMPO (RH_SHOT_TAKE), 99 frames: from ~880 (SPRAY out at 1150, bites at 1000/2000), from ~4200 (the killing
#   hit on slot 0 at 5017), and the HARD HANDS window at 13000
#
#   bash tools/asset-pipeline/foundation_films.sh [take ...]        (build the game once first; it runs --no-build)
#
# The OLD side of every comparison is the foundation study's own takes (build/shots/bite/att_current, F0, repF0: the
# crouch strip, the contact-time shove, the full-white flash), filmed before this pass from the same seed.
. "$(dirname "${BASH_SOURCE[0]}")/../shellenv.sh" || exit 1
cd "$(dirname "${BASH_SOURCE[0]}")/../.." || exit 1
OUT="${FOUNDATION_OUT:-build/shots/foundation}"
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
ANIM="$WINDIR\\assets\\art\\Animations\\Enemies"
LIGHT="umbral_swarm_attack_strip8_512=$ANIM\\choir_swarm_attack\\choir_swarm_attack_strip8_512.png;umbral_swarm_idle_strip8_512=$ANIM\\choir_swarm_idle\\choir_swarm_idle_strip8_512.png"
TEMPO="bite,swift,quick,road_sign_2,brisk,rhythm,blitz,volley"
OLDFLASH="1,0.2,200"
film() {  # name count start [ENV=VAL ...]
  local name=$1 count=$2 start=$3; shift 3
  rm -f "$OUT/${name}"_[0-9][0-9].png
  RH_ENV=(RH_SHOT_SWAP="volley_spray:volley_spray@Body" RH_SHOT_SEED=7 RH_REACTION_RECIPES=0 "$@" RH_PRESENT_TRACE=1
          RH_SHOT_HUNTER=seeker RH_SHOT_T=3.4 RH_SHOT="$WINDIR\\${OUT//\//\\}\\$name.png" RH_SHOT_MODE="${MODE:-fight}"
          RH_SHOT_SEQ="$count,1,$start")
  dn run --project src/IdleXIdle.Game --no-build >"$OUT/$name.log" 2>&1
  echo "$name: $(ls "$OUT/${name}"_[0-9][0-9].png 2>/dev/null | wc -l) frames"
}
want() { [ ${#ARGS[@]} -eq 0 ] || [[ " ${ARGS[*]} " == *" $1 "* ]]; }
ARGS=("$@")
N=99; S=115
# ── ENEMY ATTACK: the new bite as played, bare (the acceptance view: no tint, no effects, NO FLASH), silhouette, and
#    without the lunge (the strip alone) ──────────────────────────────────────────────────────────────────────────
want new           && film new           $N $S
want new_bare      && film new_bare      $N $S RH_SHOT_NOTINT=1 RH_SHOT_NOVFX=1 RH_SHOT_FLASH=off
want new_sil       && film new_sil       $N $S RH_SHOT_NOTINT=1 RH_SHOT_NOVFX=1 RH_SHOT_FLASH=off RH_SHOT_SIL=1
want new_noroot    && film new_noroot    $N $S RH_SHOT_NOTINT=1 RH_SHOT_NOVFX=1 RH_SHOT_FLASH=off RH_SHOT_NOROOT=1
want new_norecoil  && film new_norecoil  $N $S RH_SHOT_NORECOIL=1
# ── FLASH: the old full-white flash on the new foundation (everything else equal), a light body, the boss ───────
want new_oldflash  && film new_oldflash  $N $S RH_SHOT_FLASH="$OLDFLASH"
want light         && film light         $N $S RH_SHOT_STRIP_FILES="$LIGHT"
want light_oldflash && film light_oldflash $N $S RH_SHOT_STRIP_FILES="$LIGHT" RH_SHOT_FLASH="$OLDFLASH"
want boss          && MODE=boss film boss $N 30
# ── REPEATED COMBAT at fast TEMPO: SPRAY's window (bites at 1000/2000, SPRAY out at 1150), the killing hit, HARD HANDS
want rep           && film rep           $N 720 RH_SHOT_TAKE="$TEMPO"
want rep_oldflash  && film rep_oldflash  $N 720 RH_SHOT_TAKE="$TEMPO" RH_SHOT_FLASH="$OLDFLASH"
want kill          && film kill          $N 918 RH_SHOT_TAKE="$TEMPO"
want hh            && film hh            60 455 RH_SHOT_TAKE="$TEMPO"
want hh_bare       && film hh_bare       60 455 RH_SHOT_TAKE="$TEMPO" RH_SHOT_NOTINT=1 RH_SHOT_NOVFX=1 RH_SHOT_FLASH=off
exit 0
