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
  # PHASE 1 / P1.2, THE CHAMPION-AGNOSTIC TIER (trace only): the closed layers live on other champions, numbers outlined
  take p12_anvil_jaws  anvil  "sig_anvil_hardface,volley_spray@Mind,snare_jaws@Shadow,field_mire@Nature" - 0.5 20 30   # JAWS-agnostic: reaction-spawn / reaction-number, no fx_anvil_trap, no trap clip
  take p12_chorus_press chorus "sig_chorus_grave_song,hammer_press@Body,volley_spray@Mind,hammer_blow@Body" - 0.5 20 30  # PRESS-agnostic beside GRAVE SONG (held): field-wave / field-draw, no held fx_press
  take p12_tower_brand tower  "sig_tower_slow_fall,sign_brand@Machine,volley_spray@Mind,hammer_blow@Body" - 0.5 20 30   # BRAND-agnostic: mark-wave / mark-draw
  # PHASE 1 / P1.3, THE MELEE BASIC ATTACKS PERFORMED (design.md 5.18-5.27): from the wave's first beat, 150 x 2; the trace
  # holds swing-start / swing-contact / swing-step / swing-draw, the stills the contact picture (pick, then delete the rest)
  take p13_seeker    seeker    "$SEEKER"                                                  - beat:1 150 2   # the blade cut, 25 % step-in
  take p13_anvil     anvil     "sig_anvil_hardface,volley_spray@Mind,hammer_press@Body"       - beat:1 150 2   # the lunge punch, 20 %
  take p13_metronome metronome "sig_metronome_clockwork,volley_spray@Mind,hammer_press@Body"  - beat:1 150 2   # the running punch, 25 %; FIRST BEAT outline=FFFFFF
  take p13_tower     tower     "sig_tower_slow_fall,volley_spray@Mind,hammer_press@Body"      - beat:1 150 2   # the hammer slam AT the creature: dust + flat ring
  take p13_thornwall thornwall "sig_thornwall_narrows,volley_spray@Mind,hammer_press@Body"    - beat:1 150 2   # the shield bash: a broad 2-frame flash
  take p13_magpie    magpie    "sig_magpie_paying_work,volley_spray@Mind,hammer_press@Body"   - beat:1 150 2   # the dagger nick, the fastest profile
  # PHASE 1 / P1.4, THE MISSILE AND REACH BASICS (design.md 5.24-5.27): the trace holds swing-release (beat - travel, or the
  # recorded clamp) / swing-land / swing-contact / swing-strand (taut on the beat) / swing-draw alloc
  take p14_quiver    quiver    "sig_quiver_backdraw,volley_spray@Mind,hammer_press@Body"      - beat:1 150 2   # the arrow, release = beat - 200
  take p14_chorus    chorus    "sig_chorus_grave_song,volley_spray@Mind,hammer_press@Body"    - beat:1 150 2   # one bone charm, a bone clatter
  take p14_unbroken  unbroken  "sig_unbroken_hold_fast,volley_spray@Mind,hammer_press@Body"   - beat:1 150 2   # the stone chip, a 220 ms lob
  take p14_oathbound oathbound "sig_oathbound_oathmark,volley_spray@Mind,hammer_press@Body"   - beat:1 150 2   # the chain lash: taut on the beat, 120 ms recoil
  # PHASE 1 / P1.5, THE ARCHETYPE CUES (design.md section 7): the swings now voice their archetype (swing-contact cue= /
  # swing-release cue=); render each with --mp4 (or film_audio.py --wav) to hear it beside SPRAY's contact in the same fight
  take p15_seeker    seeker    "$SEEKER"                                                  - beat:1 150 2   # sfx_blade_hit 0.36 beside SPRAY's 0.50 and HARD HANDS' 0.55 in one fight
  take p15_quiver    quiver   "sig_quiver_backdraw,volley_spray@Mind,hammer_press@Body"      - beat:1 150 2   # sfx_blade_hit 0.36 + sfx_throw_release 0.18
  take p15_anvil     anvil     "sig_anvil_hardface,volley_spray@Mind,hammer_press@Body"       - beat:1 150 2   # sfx_fist_hit 0.36
  take p15_tower     tower     "sig_tower_slow_fall,volley_spray@Mind,hammer_press@Body"      - beat:1 150 2   # sfx_stone_hit 0.36
  take p15_thornwall thornwall "sig_thornwall_narrows,volley_spray@Mind,hammer_press@Body"    - beat:1 150 2   # sfx_wood_hit 0.36
  # PHASE 1 / P1.6, THE IDENTITY CUES (design.md section 7, "Basic-attack hit family"): one take per champion; each swing
  # line's cue= is now the champion's own sfx_<id>_swing_hit 0.36 (+ its own loose / toss 0.16-0.18), one sound per swing ms
  take p16_seeker    seeker    "$SEEKER"                                                  - beat:1 150 2   # sfx_seeker_swing_hit beside SPRAY / HARD HANDS
  take p16_anvil     anvil     "sig_anvil_hardface,volley_spray@Mind,hammer_press@Body"       - beat:1 150 2   # sfx_anvil_swing_hit
  take p16_metronome metronome "sig_metronome_clockwork,volley_spray@Mind,hammer_press@Body"  - beat:1 150 2   # sfx_metronome_swing_hit
  take p16_tower     tower     "sig_tower_slow_fall,volley_spray@Mind,hammer_press@Body"      - beat:1 150 2   # sfx_tower_swing_hit
  take p16_thornwall thornwall "sig_thornwall_narrows,volley_spray@Mind,hammer_press@Body"    - beat:1 150 2   # sfx_thornwall_swing_hit
  take p16_magpie    magpie    "sig_magpie_paying_work,volley_spray@Mind,hammer_press@Body"   - beat:1 150 2   # sfx_magpie_swing_hit
  take p16_quiver    quiver    "sig_quiver_backdraw,volley_spray@Mind,hammer_press@Body"      - beat:1 150 2   # sfx_quiver_swing_hit + sfx_quiver_loose 0.18
  take p16_chorus    chorus    "sig_chorus_grave_song,volley_spray@Mind,hammer_press@Body"    - beat:1 150 2   # sfx_chorus_swing_hit + sfx_chorus_toss 0.16
  take p16_unbroken  unbroken  "sig_unbroken_hold_fast,volley_spray@Mind,hammer_press@Body"   - beat:1 150 2   # sfx_unbroken_swing_hit + sfx_unbroken_toss 0.16
  take p16_oathbound oathbound "sig_oathbound_oathmark,volley_spray@Mind,hammer_press@Body"   - beat:1 150 2   # sfx_oathbound_swing_hit
  # PHASE 1 ACCEPTANCE FILM (P1.7, design.md section 8 "Film: Part 1 segment A at TEMPO 60 plus the ten swing chapters";
  # section 9 chapters 01-10): each take its chapter's WHOLE build (design.md 9 Part 2: an Active signature + BLOW@Body +
  # PRESS@Body + JAWS@Shadow, a passive signature + PRESS@Body + BLOW@Body + DRINK@Nature), from 0.6 s before the
  # wave's SECOND beat (the lead shows the wind-up). Not beat 1: the live run has already voiced beat 1's swing when the
  # seek rebuilds the replay, so on the replay that swing draws but is silent and untraced (notes.md P1.7, open issue).
  # Chapter 01 is marked (slow): it is filmed at true 60 fps (150 x 1) so its 0.25x render is made from the same frames.
  # The magpie's JAWS@Shadow is BRAND@Shadow (notes.md P1.7): the agnostic BRAND is filmed once in Phase 1, on the
  # champion design.md's Part 1 E gives it to; JAWS-agnostic is in the anvil / metronome chapters.
  take p17_a_tempo      seeker    "$SEEKER" - 3.4 150 1 RH_SHOT_TAKE="$TEMPO"                                                 # Part 1 A: the fastest TEMPO, swings yielding into SPRAY
  take p17_c01_seeker   seeker    "$SEEKER"                                                                 - beat:2 150 1 RH_SHOT_LEAD=0.6   # 01 (slow): the blade cut, true 60 fps
  take p17_c02_anvil    anvil     "sig_anvil_hardface,hammer_blow@Body,hammer_press@Body,snare_jaws@Shadow"         - beat:2 150 2 RH_SHOT_LEAD=0.6   # 02: the lunge punch
  take p17_c03_metronome metronome "sig_metronome_clockwork,hammer_blow@Body,hammer_press@Body,snare_jaws@Shadow"   - beat:2 150 2 RH_SHOT_LEAD=0.6   # 03: the running punch; FIRST BEAT's white outline
  take p17_c04_tower    tower     "sig_tower_slow_fall,hammer_press@Body,hammer_blow@Body,drain_drink@Nature"       - beat:2 150 2 RH_SHOT_LEAD=0.6   # 04: the hammer slam at the creature
  take p17_c05_thornwall thornwall "sig_thornwall_narrows,hammer_press@Body,hammer_blow@Body,drain_drink@Nature"    - beat:2 150 2 RH_SHOT_LEAD=0.6   # 05: the shield bash
  take p17_c06_magpie   magpie    "sig_magpie_paying_work,hammer_blow@Body,hammer_press@Body,sign_brand@Shadow"     - beat:2 150 2 RH_SHOT_LEAD=0.6   # 06: the dagger nick (BRAND for JAWS, see above)
  take p17_c07_quiver   quiver    "sig_quiver_backdraw,hammer_press@Body,hammer_blow@Body,drain_drink@Nature"       - beat:2 150 2 RH_SHOT_LEAD=0.6   # 07: the bow shot
  take p17_c08_chorus   chorus    "sig_chorus_grave_song,hammer_press@Body,hammer_blow@Body,drain_drink@Nature"     - beat:2 150 2 RH_SHOT_LEAD=0.6   # 08: the bone charm
  take p17_c09_unbroken unbroken  "sig_unbroken_hold_fast,hammer_press@Body,hammer_blow@Body,drain_drink@Nature"    - beat:2 150 2 RH_SHOT_LEAD=0.6   # 09: the stone chip
  take p17_c10_oathbound oathbound "sig_oathbound_oathmark,hammer_press@Body,hammer_blow@Body,drain_drink@Nature"   - beat:2 150 2 RH_SHOT_LEAD=0.6   # 10: the chain lash
  # PHASE 1 REVIEW FILM (the director's plan, 2026-10-04): A seeks SPRAY's cast with a 2.2 s lead at the fastest TEMPO; the
  # chapters seek the wave's FIRST beat (0.6 s lead shows the wind-up). Beat 1 is clean since the rig fix: a seek's replay
  # rebuild forgets the swing's voiced ms (SwingPerformance.ForgetVoiced), so the re-crossed swing is voiced and traced.
  take p1_A_seeker_t60  seeker    "$SEEKER"                                                                 - skill:volley_spray 150 2 RH_SHOT_LEAD=2.2 RH_SHOT_TAKE="$TEMPO"  # A + 01 (slow from the same frames)
  take p1_02_anvil      anvil     "sig_anvil_hardface,hammer_blow@Body,hammer_press@Body,snare_jaws@Shadow"         - beat:1 150 2 RH_SHOT_LEAD=0.6   # the lunge punch; JAWS / PRESS agnostic
  take p1_03_metronome  metronome "sig_metronome_clockwork,hammer_blow@Body,hammer_press@Body,snare_jaws@Shadow"   - beat:1 150 2 RH_SHOT_LEAD=0.6   # the running punch; FIRST BEAT
  take p1_04_tower      tower     "sig_tower_slow_fall,hammer_press@Body,hammer_blow@Body,drain_drink@Nature"       - beat:1 150 2 RH_SHOT_LEAD=0.6   # the slam at the creature
  take p1_05_thornwall  thornwall "sig_thornwall_narrows,hammer_press@Body,hammer_blow@Body,drain_drink@Nature"    - beat:1 150 2 RH_SHOT_LEAD=0.6   # the shield bash
  take p1_06_magpie     magpie    "sig_magpie_paying_work,hammer_blow@Body,hammer_press@Body,sign_brand@Shadow"     - beat:1 150 2 RH_SHOT_LEAD=0.6   # the dagger nick; BRAND agnostic (swap for JAWS)
  take p1_07_quiver     quiver    "sig_quiver_backdraw,hammer_press@Body,hammer_blow@Body,drain_drink@Nature"       - beat:1 150 2 RH_SHOT_LEAD=0.6   # the bow shot (+ a 0.25x render)
  take p1_08_chorus     chorus    "sig_chorus_grave_song,hammer_press@Body,hammer_blow@Body,drain_drink@Nature"     - beat:1 150 2 RH_SHOT_LEAD=0.6   # one bone charm
  take p1_09_unbroken   unbroken  "sig_unbroken_hold_fast,hammer_press@Body,hammer_blow@Body,drain_drink@Nature"    - beat:1 150 2 RH_SHOT_LEAD=0.6   # the stone chip lob
  take p1_10_oathbound  oathbound "sig_oathbound_oathmark,hammer_press@Body,hammer_blow@Body,drain_drink@Nature"   - beat:1 150 2 RH_SHOT_LEAD=0.6   # the chain lash (+ a 0.25x render)
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
