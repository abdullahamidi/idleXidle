#!/bin/bash
# ENEMY ATTACK READABILITY + HIT FEEDBACK STUDY (2026-09-26), THROWAWAY: film every take the study compares.
# One seeded fight (RH_SHOT_SEED=7, the JAWS review's: the Seeker vs four GLOOM WHELPS in Umbral Reach), the reaction
# layer OFF (the legacy answer: number + flash on the bite's own frame), normal TEMPO, 99 frames from ~6500 ms: the
# Seeker's swing lands at 6700 (150 on the front whelp), the pack bites at 7000, JAWS answers on the same frame.
#   bash prototypes/bite-readability/film.sh [take ...]          (build the game once first; it runs --no-build)
. "$(dirname "${BASH_SOURCE[0]}")/../../tools/shellenv.sh" || exit 1
cd "$(dirname "${BASH_SOURCE[0]}")/../.." || exit 1
OUT="${BITE_OUT:-build/shots/bite}"
mkdir -p "$OUT"
# The legacy row ring (fx_seeker_trap, and its fallback fx_trap) is the OLD JAWS presentation's, and with the reaction
# layer off it is drawn over the pack at every answer. As in the concept study's plates it is moved out of the BUILD
# OUTPUT for the takes and put back on exit (never a repo file), so the answer is its number and flash alone.
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
PROTO="$WINDIR\\build\\shots\\bite\\proto\\umbral_swarm_attack_strip8_512.png"
ANIM="$WINDIR\\assets\\art\\Animations\\Enemies"
LIGHT="umbral_swarm_attack_strip8_512=$ANIM\\choir_swarm_attack\\choir_swarm_attack_strip8_512.png;umbral_swarm_idle_strip8_512=$ANIM\\choir_swarm_idle\\choir_swarm_idle_strip8_512.png"
TEMPO="bite,swift,quick,road_sign_2,brisk,rhythm,blitz,volley"
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
# ── PART A: the bite as it is (JAWS' answer hidden so the whelp's own act is what is on screen) ────────────────
want att_current  && film att_current  $N $S RH_SHOT_NOANSWER=1
want att_notint   && film att_notint   $N $S RH_SHOT_NOANSWER=1 RH_SHOT_NOTINT=1
want att_bare     && film att_bare     $N $S RH_SHOT_NOANSWER=1 RH_SHOT_NOTINT=1 RH_SHOT_NOVFX=1
want att_sil      && film att_sil      $N $S RH_SHOT_NOANSWER=1 RH_SHOT_NOTINT=1 RH_SHOT_NOVFX=1 RH_SHOT_SIL=1
# ── PART B: the key-pose prototype (A: art only) and art + a restrained lunge (B), bare and full ───────────────
want artA_bare    && film artA_bare    $N $S RH_SHOT_NOANSWER=1 RH_SHOT_NOTINT=1 RH_SHOT_NOVFX=1 RH_SHOT_STRIP_FILES="umbral_swarm_attack_strip8_512=$PROTO"
want artA_full    && film artA_full    $N $S RH_SHOT_NOANSWER=1 RH_SHOT_STRIP_FILES="umbral_swarm_attack_strip8_512=$PROTO"
want rootB_bare   && film rootB_bare   $N $S RH_SHOT_NOANSWER=1 RH_SHOT_NOTINT=1 RH_SHOT_NOVFX=1 RH_SHOT_STRIP_FILES="umbral_swarm_attack_strip8_512=$PROTO" RH_SHOT_BITE=root
want rootB_full   && film rootB_full   $N $S RH_SHOT_NOANSWER=1 RH_SHOT_STRIP_FILES="umbral_swarm_attack_strip8_512=$PROTO" RH_SHOT_BITE=root
want rootF_bare   && film rootF_bare   $N $S RH_SHOT_NOANSWER=1 RH_SHOT_NOTINT=1 RH_SHOT_NOVFX=1 RH_SHOT_STRIP_FILES="umbral_swarm_attack_strip8_512=$PROTO" RH_SHOT_BITE=rootfront
want rootonly_bare && film rootonly_bare $N $S RH_SHOT_NOANSWER=1 RH_SHOT_NOTINT=1 RH_SHOT_NOVFX=1 RH_SHOT_BITE=root
# a DIAGNOSTIC, not a third variant: B with twice the travel (the front whelp closes ~76 px). Is the amount the problem?
want rootB2_bare  && film rootB2_bare  $N $S RH_SHOT_NOANSWER=1 RH_SHOT_NOTINT=1 RH_SHOT_NOVFX=1 RH_SHOT_STRIP_FILES="umbral_swarm_attack_strip8_512=$PROTO" RH_SHOT_BITE=root:2
want rootB2_full  && film rootB2_full  $N $S RH_SHOT_NOANSWER=1 RH_SHOT_STRIP_FILES="umbral_swarm_attack_strip8_512=$PROTO" RH_SHOT_BITE=root:2
# ── PART C: the flash, one hit under four grammars: the swing at 6700 and the answer at 7000 ────────────────────
want F0           && film F0 $N $S
want F1           && film F1 $N $S RH_SHOT_FLASH=F1
want F2           && film F2 $N $S RH_SHOT_FLASH=F2
want F3           && film F3 $N $S RH_SHOT_FLASH=F3
# a LIGHT body in the same fight (the Choir swarm's strips stand in for the whelp's), and the BOSS fixture
want lightF0      && film lightF0 $N $S RH_SHOT_STRIP_FILES="$LIGHT"
want lightF1      && film lightF1 $N $S RH_SHOT_STRIP_FILES="$LIGHT" RH_SHOT_FLASH=F1
want lightF2      && film lightF2 $N $S RH_SHOT_STRIP_FILES="$LIGHT" RH_SHOT_FLASH=F2
want lightF3      && film lightF3 $N $S RH_SHOT_STRIP_FILES="$LIGHT" RH_SHOT_FLASH=F3
want bossF0       && MODE=boss film bossF0 $N ${BOSS_S:-30}
want bossF1       && MODE=boss film bossF1 $N ${BOSS_S:-30} RH_SHOT_FLASH=F1
want bossF2       && MODE=boss film bossF2 $N ${BOSS_S:-30} RH_SHOT_FLASH=F2
want bossF3       && MODE=boss film bossF3 $N ${BOSS_S:-30} RH_SHOT_FLASH=F3
# REPEATED HITS: fast TEMPO, SPRAY, HARD HANDS, swings and bites inside 1.6 s
want repF0        && film repF0 $N 720 RH_SHOT_TAKE="$TEMPO"
want repF1        && film repF1 $N 720 RH_SHOT_TAKE="$TEMPO" RH_SHOT_FLASH=F1
want repF2        && film repF2 $N 720 RH_SHOT_TAKE="$TEMPO" RH_SHOT_FLASH=F2
want repF3        && film repF3 $N 720 RH_SHOT_TAKE="$TEMPO" RH_SHOT_FLASH=F3
# ── the optional POSE HOLD comparison (never on by default): 50 ms on the best attack + F2 ───────────────────────
want hold50       && film hold50 $N $S RH_SHOT_NOANSWER=1 RH_SHOT_STRIP_FILES="umbral_swarm_attack_strip8_512=$PROTO" RH_SHOT_BITE=root RH_SHOT_FLASH=F2 RH_SHOT_HITSTOP=50
# ── PART D: the best attack + the best flash, the answer ON (its number and flash on the bite frame), for A/B/C ──
want best_plate   && film best_plate   $N $S RH_SHOT_STRIP_FILES="umbral_swarm_attack_strip8_512=$PROTO" RH_SHOT_BITE=root RH_SHOT_FLASH=F2
want best_plateF3 && film best_plateF3 $N $S RH_SHOT_STRIP_FILES="umbral_swarm_attack_strip8_512=$PROTO" RH_SHOT_BITE=root RH_SHOT_FLASH=F3
want best_rep     && film best_rep     $N 720 RH_SHOT_TAKE="$TEMPO" RH_SHOT_STRIP_FILES="umbral_swarm_attack_strip8_512=$PROTO" RH_SHOT_BITE=root RH_SHOT_FLASH=F2
exit 0
