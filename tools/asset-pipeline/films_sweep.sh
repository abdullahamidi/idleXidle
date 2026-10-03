#!/usr/bin/env bash
# tools/asset-pipeline/films_sweep.sh [--mp4] [--keep] [--dry] <tag> <take|--take "SPEC"> ...
# The REMAINING-SKILL PRESENTATION SWEEP's film rig (production/vfx-sweep/design.md section 9): one take per process,
# seeded (RH_SHOT_SEED=7) and traced (RH_PRESENT_TRACE=1, the trace kept per take as <name>.log), generalising
# films_press.sh / films_brand.sh. Output: build/shots/sweep/<tag>/<name>_NN.png + <name>.log (+ <name>.mp4 with --mp4).
#
# A take is one line of the table below (or an ad-hoc `--take "SPEC"`), SPEC being
#     <name> <hunter> <build|-> <variation|-> <seek|-> <count> <stride> [ENV=VAL ...]
#   hunter     RH_SHOT_HUNTER (- leaves the fixture's)
#   build      RH_SHOT_BUILD: the take's WHOLE build, `<signature>,<skill>[@Source],...` (the signature first, at most two
#              Active and two passives; a Source left out is Body, or a chosen variation's own). Refused loudly: the game
#              exits non-zero on a build the rule refuses, a slot the loadout declines, or a live run that differs (P0.2).
#              An RH_SHOT_SWAP=... among the ENV values is still read when this column is `-` (never beside a build).
#   variation  RH_SHOT_VARIATION (`skill:VAR+VAR`)
#   seek       RH_SHOT_SEEK, an EVENT of the wave (skill:<id>[#n][+struck|+unstruck][+ontick], aura:<id>[#n], down[#n],
#              hit:<HitSource>[#n], downing[#n], beat:<n>, event:<Kind>[#n]; RH_SHOT_LEAD seconds before it); a wave without
#              it exits non-zero. A plain number is still RH_SHOT_T (seconds into the fixture).
#   count      saved frames, AT MOST 150 (trimmed takes: the owner's disk filled twice)
#   stride     one saved frame every <stride> drawn frames (the trace covers every frame either way)
#   ENV=VAL    anything else; RH_SHOT_MODE=<mode> picks the capture mode (default `fight`)
#
# --mp4   render <name>.mp4 with the traced sound (film_audio.py) before the frames are deleted
# --keep  keep the frames (default: `find <dir> -name '*.png' -delete` right after each take, 3-digit names included)
# --dry   print each take's environment, film nothing
#
# DISK GUARD (films_brand.sh's, verbatim, plus three rules): name the takes you need, never the whole set; refuse while
# the frame folders already hold more than 2 GB or the drive has under 20 GB free; refuse a take of more than 150
# frames; check again between takes and STOP once build/shots/sweep holds more than 500 MB of temp files.
cd "$(dirname "${BASH_SOURCE[0]}")/../.." || exit 1
MP4=0; KEEP=0; DRY=0; ADHOC=()
while [ $# -gt 0 ]; do
  case "$1" in
    --mp4) MP4=1; shift ;;
    --keep) KEEP=1; shift ;;
    --dry) DRY=1; shift ;;
    *) break ;;
  esac
done
TAG=${1:-}; shift
[ -n "$TAG" ] || { echo "films_sweep: usage: films_sweep.sh [--mp4] [--keep] [--dry] <tag> <take ...>"; exit 1; }
ARGS=()
while [ $# -gt 0 ]; do
  case "$1" in
    --take) [ -n "${2:-}" ] || { echo "films_sweep: --take needs a SPEC"; exit 1; }; ADHOC+=("$2"); ARGS+=("${2%% *}"); shift 2 ;;
    --mp4) MP4=1; shift ;;
    --keep) KEEP=1; shift ;;
    --dry) DRY=1; shift ;;
    *) ARGS+=("$1"); shift ;;
  esac
done
SWEEP=build/shots/sweep
OUT=$SWEEP/$TAG
MAX_FRAMES=150
TEMP_LIMIT_MB=500
# a guard threshold can be RAISED for a self-test (SWEEP_GUARD_FREE_MB=999999999 proves the full-disk refusal), never lowered
FREE_MIN_MB=20480; [ "${SWEEP_GUARD_FREE_MB:-0}" -gt "$FREE_MIN_MB" ] 2>/dev/null && FREE_MIN_MB=$SWEEP_GUARD_FREE_MB

# DISK GUARD (the owner, 2026-10-02: the frames filled C: twice): name the takes you need, never the whole set;
# refuse while the frame folders already hold more than 2 GB or C: has under 20 GB free. Delete frames once used.
[ ${#ARGS[@]} -gt 0 ] || { echo "films_sweep: name the takes to film (no default full set)"; exit 1; }
mkdir -p build/shots
held=$(du -sm build/shots | cut -f1); free=$(df -m . | awk 'NR==2{print $4}')
[ "$held" -le 2048 ] || { echo "films_sweep: build/shots holds ${held} MB - delete used frames first"; exit 1; }
[ "$free" -ge "$FREE_MIN_MB" ] || { echo "films_sweep: only ${free} MB free on this drive - refusing"; exit 1; }

# the fixtures the takes share (films_brand.sh's); since P0.2 each is the WHOLE build the swap used to leave behind
TEMPO="bite,swift,quick,road_sign_2,brisk,rhythm,blitz,volley"          # RH_SHOT_TAKE: the fastest TEMPO segment
SEEKER="sig_seeker_hard_hands,volley_spray@Mind,hammer_press@Body,snare_jaws@Shadow"      # the default fight fixture
BRAND_BASE="sig_seeker_hard_hands,volley_spray@Body,sign_brand@Shadow,snare_jaws@Shadow"  # BRAND in PRESS's slot, JAWS kept
BRAND_PRESS="sig_seeker_hard_hands,volley_spray@Body,hammer_press@Body,sign_brand@Shadow" # BRAND in JAWS's slot, PRESS kept

build_env() { [ "$1" = "-" ] || echo "RH_SHOT_BUILD=$1"; }
seek_env()  { [ "$1" = "-" ] && return 0; if [[ "$1" =~ ^[0-9]+(\.[0-9]+)?$ ]]; then echo "RH_SHOT_T=$1"; else echo "RH_SHOT_SEEK=$1"; fi; }

want() { [[ " ${ARGS[*]} " == *" $1 "* ]]; }
PHASE=plan; KNOWN=" "; FAILED=0
take() {  # name hunter build variation seek count stride [ENV=VAL ...]
  [ $# -ge 7 ] || { echo "films_sweep: a take needs 7 columns: $*"; exit 1; }
  local name=$1 hunter=$2 build=$3 variation=$4 seek=$5 count=$6 stride=$7; shift 7
  KNOWN+="$name "
  want "$name" || return 0
  if [ "$PHASE" = plan ]; then
    [[ "$count" =~ ^[0-9]+$ && "$stride" =~ ^[0-9]+$ ]] || { echo "films_sweep: $name: count / stride must be numbers"; exit 1; }
    [ "$count" -le $MAX_FRAMES ] || { echo "films_sweep: $name asks for $count frames - a take is at most $MAX_FRAMES (trim it)"; exit 1; }
    return 0
  fi
  local temp; temp=$(du -sm "$SWEEP" 2>/dev/null | cut -f1)
  [ "${temp:-0}" -le $TEMP_LIMIT_MB ] || { echo "films_sweep: $SWEEP holds ${temp} MB of temp - stopping before $name"; exit 1; }
  local mode=fight envs=(RH_SHOT_SEED=7 RH_PRESENT_TRACE=1) kv
  [ "$hunter" = "-" ] || envs+=("RH_SHOT_HUNTER=$hunter")
  kv=$(build_env "$build"); [ -z "$kv" ] || envs+=("$kv")
  [ "$variation" = "-" ] || envs+=("RH_SHOT_VARIATION=$variation")
  kv=$(seek_env "$seek"); [ -z "$kv" ] || envs+=("$kv")
  for kv in "$@"; do
    case "$kv" in
      RH_SHOT_MODE=*) mode=${kv#RH_SHOT_MODE=} ;;
      *=*) envs+=("$kv") ;;
      *) echo "films_sweep: $name: '$kv' is not ENV=VAL"; exit 1 ;;
    esac
  done
  if [ $DRY = 1 ]; then echo "$name: mode=$mode ${count}x$stride ${envs[*]}"; return 0; fi
  mkdir -p "$OUT"
  echo "films_sweep: $name (mode=$mode ${count}x$stride ${envs[*]})"
  env "${envs[@]}" RH_SEQ_LOG="$PWD/$OUT/$name.log" timeout 2400 \
    bash tools/asset-pipeline/capture_seq.sh "$mode" "$count" "$stride" "$OUT/$name" 2>&1 | tail -1
  local rc=${PIPESTATUS[0]} ok=1
  if [ "$rc" != 0 ] || ! ls "$OUT/$name"_[0-9]*.png >/dev/null 2>&1; then
    echo "films_sweep: $name FAILED (exit $rc, no film)"
    # the rig's loud refusal (RH_SHOT_BUILD / RH_SHOT_SEEK / RH_SHOT_KEYSTONES ...), as the game printed it
    grep -m1 -o "InvalidOperationException: .*" "$OUT/$name.log" 2>/dev/null | sed 's/^/films_sweep:   /'
    FAILED=1; ok=0
  fi
  if [ $MP4 = 1 ] && [ $ok = 1 ]; then
    py tools/asset-pipeline/film_audio.py "$OUT/$name.log" "$OUT/$name" "$OUT/$name.mp4" 2>&1 | tail -1
  fi
  if [ $KEEP = 0 ]; then find "$OUT" -name '*.png' -delete; fi
  echo "films_sweep: $name done; $SWEEP holds $(du -sm "$SWEEP" | cut -f1) MB"
}
. tools/shellenv.sh || exit 1

takes() {
  # THE REFERENCE BASELINE (Phase 0, P0.1): the Seeker's five closed references, TRACE-FIRST (few pictures, long cover).
  # Run each twice and hold them with `action_regression.py --refs a.log b.log`.
  take ref_seeker      seeker "$SEEKER"      - 0.5 40 15                       # HARD HANDS, SPRAY@Mind, PRESS@Body, JAWS@Shadow (the default fixture), 10 s
  take ref_fast        seeker "$SEEKER"      - 3.4 40 6  RH_SHOT_TAKE="$TEMPO" # the fastest TEMPO: SPRAY / HARD HANDS on the field ticks
  take ref_brand       seeker "$BRAND_BASE"  - 0.5 40 15                       # SPRAY@Body, BRAND@Shadow in PRESS's slot, JAWS kept
  take ref_brand_press seeker "$BRAND_PRESS" - 0.5 40 15                       # PRESS + BRAND on one body, JAWS out
  take ref_jaws_kill   seeker "$SEEKER"      - 0.5 40 10 RH_SHOT_ENEMY=20,420  # JAWS' killing answer (jaws-base evidence 12a/b)
  # THE BEFORE FILMS for P0.6 (the generic path's defects, filmed at the clean HEAD): fightmulti seeks SPRAY itself
  take before_multi    seeker -                                   - - 150 2 RH_SHOT_MODE=fightmulti
  take before_bleed    seeker - - - 150 2 RH_SHOT_MODE=fightmulti RH_SHOT_SWAP=snare_jaws:volley_weep@Shadow  # WEEP for JAWS: the bleed strobe (as filmed at ae20a7fd)
  # THE AFTER FILMS (Phase 0 acceptance, P0.6): the same poses at the Phase 0 code, plus one take per generic rule
  take after_multi     seeker -                                   - - 150 2 RH_SHOT_MODE=fightmulti                # vs before_multi: one sfx_hit per batch ms
  take after_bleed     seeker - - - 150 2 RH_SHOT_MODE=fightmulti RH_SHOT_SWAP=snare_jaws:volley_weep@Shadow  # vs before_bleed: quiet bleed, no strobe
  take after_default   seeker "$SEEKER" - 0.5 150 4                                                             # the default fixture, 10 s (callouts SPRAY / HARD HANDS)
  take after_backdraw  quiver "sig_quiver_backdraw,hammer_press@Body,volley_spray@Mind,hammer_blow@Body" - skill:sig_quiver_backdraw+unstruck 150 2  # the phantom: skill-skip
  take after_weaver    seeker "$SEEKER" - 9 150 2 RH_SHOT_KEYSTONES=weaver                                      # the woven echo at 11200: echo, no second cast breath
  take after_drink     seeker "sig_seeker_hard_hands,drain_drink@Nature,hammer_press@Body,snare_jaws@Shadow" - skill:drain_drink 150 2 RH_SHOT_ENEMY=1500,25  # the summed +N, no heal column
  take after_holdfast  unbroken - sig_unbroken_hold_fast:BREASTWORK+GROUNDWORK - 150 8                          # the wave-open plate: rim only, no cue
  # THE DIRECTOR'S REVIEW FILM of Phase 0 (production/vfx-sweep/review/phase0/INDEX.md): the BEFORE takes are the P0.1
  # films (phase0-before/*.mp4, ae20a7fd); these are the AFTER takes, each with a contact strip made by review_take.py
  take r_after_multi   seeker - - - 150 2 RH_SHOT_MODE=fightmulti                                      # SPRAY's 5-hit cast
  take r_after_bleed   seeker "sig_seeker_hard_hands,volley_spray@Mind,hammer_press@Body,volley_weep@Shadow" - hit:Bleed 150 2 RH_SHOT_LEAD=1.0  # bleed ticks quiet
  take r_after_fast    seeker "$SEEKER" - 3.4 150 1 RH_SHOT_TAKE="$TEMPO"                                       # true 60 fps, 2.5 s under load
  take r_after_pulse   seeker "sig_seeker_hard_hands,field_pulse@Spirit,field_mire@Nature,hammer_press@Body" - skill:field_pulse+ontick 150 2 RH_SHOT_LEAD=0.6 RH_SHOT_TAKE=bite,swift,quick,road_sign_2,brisk,rhythm,blitz  # the last-owner rule
  take r_after_pulse_slow seeker "sig_seeker_hard_hands,field_pulse@Spirit,field_mire@Nature,hammer_press@Body" - skill:field_pulse+ontick 40 1 RH_SHOT_LEAD=0.25 RH_SHOT_TAKE=bite,swift,quick,road_sign_2,brisk,rhythm,blitz  # the tick frame, for a 0.25x render
  take r_after_backdraw quiver "sig_quiver_backdraw,hammer_press@Body,volley_spray@Mind,hammer_blow@Body" - skill:sig_quiver_backdraw+unstruck 120 2 RH_SHOT_LEAD=0.6 RH_SHOT_ENEMY=800,9  # no phantom on the last kill
  take r_after_oathmark oathbound "sig_oathbound_oathmark,hammer_press@Body,volley_spray@Mind,hammer_blow@Body" - skill:sig_oathbound_oathmark 120 2 RH_SHOT_LEAD=0.6  # a no-damage reaction
  take r_after_drink   seeker "sig_seeker_hard_hands,drain_drink@Nature,hammer_press@Body,snare_jaws@Shadow" - skill:drain_drink 150 2 RH_SHOT_LEAD=0.6 RH_SHOT_ENEMY=1500,25  # the summed +N
  take r_after_holdfast unbroken "sig_unbroken_hold_fast,snare_jaws@Spirit,hammer_blow@Body,snare_repay@Machine" snare_repay:BANKED+CARRIED beat:1 150 2 RH_SHOT_LEAD=1.0 RH_SHOT_ARCHETYPE=Swarm  # the wave-open plate vs Fast biters
  take r_weaver_trace  seeker "$SEEKER" - 9 20 30 RH_SHOT_KEYSTONES=weaver                                     # trace only: the woven echo
  # THE PHASE 0 CORRECTION PASS (the director's review): the echo WITH frames (its flash peak only shows in a picture),
  # and HOLD FAST's damaging legacy reaction as a trace (one cue per event at the ask)
  take r_weaver        seeker "$SEEKER" - 9 150 2 RH_SHOT_KEYSTONES=weaver                                      # the woven echo at 11200, at echo scale
  take r_holdfast_trace unbroken "sig_unbroken_hold_fast,snare_jaws@Spirit,hammer_blow@Body,snare_repay@Machine" snare_repay:BANKED+CARRIED beat:1 20 30 RH_SHOT_LEAD=1.0 RH_SHOT_ARCHETYPE=Swarm  # trace only
}
takes
for s in "${ADHOC[@]}"; do eval "take $s"; done
for a in "${ARGS[@]}"; do
  [[ "$KNOWN" == *" $a "* ]] || { echo "films_sweep: no take named '$a'"; exit 1; }
done
PHASE=film
takes
for s in "${ADHOC[@]}"; do eval "take $s"; done
[ $FAILED = 0 ] || { echo "films_sweep: a take FAILED"; exit 1; }
exit 0
