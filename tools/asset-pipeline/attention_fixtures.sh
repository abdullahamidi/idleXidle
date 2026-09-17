#!/bin/bash
# THE ATTENTION PASS, PHOTOGRAPHED — every state the 2026-09-15 pass created or changed, at
# 100 / 125 / 150, plus Reduced Motion where the state has motion in it.
#
#   bash tools/asset-pipeline/attention_fixtures.sh [filter]
#
# With no filter the whole matrix runs (about seventy captures, ~40 s each). A filter is a plain
# substring matched against "<dir>/<state>", so one family can be re-taken on its own:
#
#   bash tools/asset-pipeline/attention_fixtures.sh death        # the fall and the transition
#   bash tools/asset-pipeline/attention_fixtures.sh dispatches   # the inbox, the envelope, the letters
#   bash tools/asset-pipeline/attention_fixtures.sh opening      # only the two opening-rig films
#
# Each row is  <dir>|<state>|<mode>|<scale>|<dials>  and lands at
# production/qa/evidence/<dir>/<state>_<scale>.png — with _reduced appended when the row poses
# REDUCED MOTION, which is the naming the death captures already used. Dials are ';'-separated and
# go to the game through RH_ENV, as polish_shots.sh does, so a dial capture.sh does not forward
# still reaches the Windows process (RH_SHOT_T, RH_SHOT_CANVAS_MOUSE, RH_SHOT_OPENING …).
#
# The shell must export LOCALAPPDATA (tools/shellenv.sh reads it to find the Windows dotnet):
#
#   LOCALAPPDATA=/mnt/c/Users/<you>/AppData/Local bash tools/asset-pipeline/attention_fixtures.sh
#
# THE OUT PATH IS REPO-RELATIVE. RH_SHOT is handed <repo>\<out>, and an absolute path fails with a
# bare "capture failed".
#
# THE END OF THE OPENING IS NOT A CAPTURE. Four rail tiles break on the single frame the last
# authored card is answered, and the shutter poses one frame of a fixture that never played an
# opening — so that state comes from the opening rig's own film (tools/check_opening_flow.sh), and
# the rows tagged `opening` below run it and copy the break frame in.
set -u
. "$(dirname "${BASH_SOURCE[0]}")/../shellenv.sh" || exit 1
cd "$(dirname "${BASH_SOURCE[0]}")/../.." || exit 1
FILTER="${1:-}"
SHOT_LOG="build/shots/attention/.last_run.log"; export SHOT_LOG
mkdir -p "$(dirname "$SHOT_LOG")"
WINDIR="$(winpath "$PWD")"

# ── THE MATRIX ────────────────────────────────────────────────────────────────────────────────
#
# A  FIRST FALL            the collapse, then the transition swept 0.25 / 0.5 / 0.75 / 1, and the
#                          two hover poses that prove what refuses under the black and what does not
# B  FIRST FAILURE LESSON  the transition finished, READ THE LOG allowed to land
# C  LOG OPEN              the report being read, with MAKE ONE CHANGE forced and refused
# D  POST-LOG CHANGE       the same lesson with the log shut, guiding normally
# E  CHEST + DISPATCH      a letter posted DURING a reveal, and the same fixture after it
# F  DISPATCHES            the inbox: mixed, all-read, empty, the mark, the hover, the lesson, a
#                          scrolled column, and the two sentences a Vow can arrive with
# G  MIGRATED VETERAN      `dispatchesempty` is the picture; the proof is check_boot.sh's lane
#                          (the ledger line is quoted in the evidence README)
# +  FAMILIES              one fixture per family, to see the envelope's mark as the only change
# +  FLOURISH              the NEW dot the coach is pointing at (halo) beside the same dot under a
#                          higher owner (no halo), and the end of the opening
ROWS=(
  # ── A. FIRST FALL ──
  "attention/death|fightfall_060|fightfall|100|RH_SHOT_T=0.6"
  "attention/death|fightfall_060|fightfall|125|RH_SHOT_T=0.6"
  "attention/death|fightfall_060|fightfall|150|RH_SHOT_T=0.6"
  "attention/death|fightfall_060_hover|fightfall|100|RH_SHOT_T=0.6;RH_SHOT_CANVAS_MOUSE=1457,118"
  "attention/death|fightfade_025|fightfade|100|RH_SHOT_T=0.25"
  "attention/death|fightfade_025|fightfade|125|RH_SHOT_T=0.25"
  "attention/death|fightfade_025|fightfade|150|RH_SHOT_T=0.25"
  "attention/death|fightfade_050|fightfade|100|RH_SHOT_T=0.5"
  "attention/death|fightfade_050|fightfade|125|RH_SHOT_T=0.5"
  "attention/death|fightfade_050|fightfade|150|RH_SHOT_T=0.5"
  "attention/death|fightfade_075|fightfade|100|RH_SHOT_T=0.75"
  "attention/death|fightfade_075|fightfade|125|RH_SHOT_T=0.75"
  "attention/death|fightfade_075|fightfade|150|RH_SHOT_T=0.75"
  "attention/death|fightfade_100|fightfade|100|RH_SHOT_T=1"
  "attention/death|fightfade_100|fightfade|125|RH_SHOT_T=1"
  "attention/death|fightfade_100|fightfade|150|RH_SHOT_T=1"
  "attention/death|fightfade_025_hover|fightfade|100|RH_SHOT_T=0.25;RH_SHOT_CANVAS_MOUSE=1457,118"
  "attention/death|fightfade_025|fightfade|100|RH_SHOT_T=0.25;RH_SHOT_REDUCED=1"
  "attention/death|fightfade_025|fightfade|125|RH_SHOT_T=0.25;RH_SHOT_REDUCED=1"
  "attention/death|fightfade_025|fightfade|150|RH_SHOT_T=0.25;RH_SHOT_REDUCED=1"
  "attention/death|fightfade_075|fightfade|100|RH_SHOT_T=0.75;RH_SHOT_REDUCED=1"
  "attention/death|fightfade_075|fightfade|125|RH_SHOT_T=0.75;RH_SHOT_REDUCED=1"
  "attention/death|fightfade_075|fightfade|150|RH_SHOT_T=0.75;RH_SHOT_REDUCED=1"

  # ── B. FIRST FAILURE LESSON, after the transition ──
  "attention/owner|fightfade_100_lesson|fightfade|100|RH_SHOT_T=1;RH_SHOT_LESSON=FirstFailureReport"
  "attention/owner|fightfade_100_lesson|fightfade|125|RH_SHOT_T=1;RH_SHOT_LESSON=FirstFailureReport"
  "attention/owner|fightfade_100_lesson|fightfade|150|RH_SHOT_T=1;RH_SHOT_LESSON=FirstFailureReport"

  # ── C. LOG OPEN, with MAKE ONE CHANGE forced ──
  "attention/owner|runlog_lesson|runlog|100|RH_SHOT_LESSON=FirstPostFailureChange"
  "attention/owner|runlog_lesson|runlog|125|RH_SHOT_LESSON=FirstPostFailureChange"
  "attention/owner|runlog_lesson|runlog|150|RH_SHOT_LESSON=FirstPostFailureChange"

  # ── D. THE SAME LESSON WITH THE LOG SHUT ──
  "attention/owner|fight_changelesson|fight|100|RH_SHOT_LESSON=FirstPostFailureChange"
  "attention/owner|fight_changelesson|fight|125|RH_SHOT_LESSON=FirstPostFailureChange"
  "attention/owner|fight_changelesson|fight|150|RH_SHOT_LESSON=FirstPostFailureChange"

  # ── E. CHEST + WAITING DISPATCH ──
  "dispatches|chestdispatch_during|chestdispatch|100|RH_SHOT_T=1.2"
  "dispatches|chestdispatch_during|chestdispatch|125|RH_SHOT_T=1.2"
  "dispatches|chestdispatch_during|chestdispatch|150|RH_SHOT_T=1.2"
  "dispatches|chestdispatch_after|chestdispatch|100|RH_SHOT_T=5"
  "dispatches|chestdispatch_after|chestdispatch|125|RH_SHOT_T=5"
  "dispatches|chestdispatch_after|chestdispatch|150|RH_SHOT_T=5"

  # ── F. DISPATCHES ──
  "dispatches|dispatches|dispatches|100|"
  "dispatches|dispatches|dispatches|125|"
  "dispatches|dispatches|dispatches|150|"
  "dispatches|dispatches_allread|dispatches|100|RH_SHOT_DISPATCH=read"
  "dispatches|dispatches_allread|dispatches|125|RH_SHOT_DISPATCH=read"
  "dispatches|dispatches_allread|dispatches|150|RH_SHOT_DISPATCH=read"
  "dispatches|dispatchesempty|dispatchesempty|100|"
  "dispatches|dispatchesempty|dispatchesempty|125|"
  "dispatches|dispatchesempty|dispatchesempty|150|"
  "dispatches|dispatchesunread|dispatchesunread|100|"
  "dispatches|dispatchesunread|dispatchesunread|125|"
  "dispatches|dispatchesunread|dispatchesunread|150|"
  # The envelope's own centre at each profile (DispatchButton, canvas space) — the cursor is parked
  # off-canvas under the rig, so a hover has to be posed at the rectangle the chain actually laid out.
  "dispatches|dispatcheshover|dispatcheshover|100|RH_SHOT_CANVAS_MOUSE=1766,46"
  "dispatches|dispatcheshover|dispatcheshover|125|RH_SHOT_CANVAS_MOUSE=1734,53"
  "dispatches|dispatcheshover|dispatcheshover|150|RH_SHOT_CANVAS_MOUSE=1702,61"
  "dispatches|dispatchlesson|dispatchesunread|100|RH_SHOT_LESSON=FirstDispatchOpened"
  "dispatches|dispatchlesson|dispatchesunread|125|RH_SHOT_LESSON=FirstDispatchOpened"
  "dispatches|dispatchlesson|dispatchesunread|150|RH_SHOT_LESSON=FirstDispatchOpened"
  "dispatches|dispatchesmany_scrolled|dispatches|100|RH_SHOT_DISPATCH=many;RH_SHOT_SCROLL=end"
  "dispatches|dispatchesmany_scrolled|dispatches|150|RH_SHOT_DISPATCH=many;RH_SHOT_SCROLL=end"
  # THE TWO SENTENCES A VOW ARRIVES WITH — granted is OFFERED TO YOU, proved REVEALED ITSELF.
  "dispatches|dispatch_vow_granted|dispatches|100|RH_SHOT_DISPATCH=vow.vow_complete"
  "dispatches|dispatch_vow_granted|dispatches|150|RH_SHOT_DISPATCH=vow.vow_complete"
  "dispatches|dispatch_vow_proved|dispatches|100|RH_SHOT_DISPATCH=vow.vow_bluntedge"
  "dispatches|dispatch_vow_proved|dispatches|150|RH_SHOT_DISPATCH=vow.vow_bluntedge"

  # ── ONE FIXTURE PER FAMILY, re-taken: the envelope's mark is the only thing that moved ──
  "dispatches/producers|family_fight|fight|100|"
  "dispatches/producers|family_lootforge|lootforge|100|RH_SHOT_T=1.2"
  "dispatches/producers|family_warren|warren|100|"
  "dispatches/producers|family_forge|forge|100|"
  # THE CHAIN AT 150 WITH SIX ORNATE CAPSULES, over the longest screen titles — the known overrun.
  "dispatches/producers|chain_sixpills_forge|forge|150|"
  "dispatches/producers|chain_longtitle_mastery|buildtree|150|"

  # ── THE COACH-TIER EXCEPTION: a dot the coach points at keeps its breath ──
  "attention/flourish|coachdot_coach|dispatchesunread|100|RH_SHOT_LESSON=FirstPostFailureChange"
  "attention/flourish|coachdot_coach|dispatchesunread|150|RH_SHOT_LESSON=FirstPostFailureChange"
  "attention/flourish|coachdot_modal|dispatchesunread|100|RH_SHOT_OPENING=IntroduceHealth"
  "attention/flourish|coachdot_modal|dispatchesunread|150|RH_SHOT_OPENING=IntroduceHealth"
  # The control the halo is judged against: same dot, same brightness, no breath at all.
  "attention/flourish|coachdot|dispatchesunread|100|RH_SHOT_REDUCED=1"
)

dn build src/IdleXIdle.Game -v q --nologo >/dev/null 2>&1 || { echo "build failed" >&2; exit 1; }

taken=0; failed=0
for row in "${ROWS[@]}"; do
  IFS='|' read -r dir state mode sc extra <<<"$row"
  [ -n "$FILTER" ] && [[ "$dir/$state" != *"$FILTER"* ]] && continue
  suffix=""; [[ "$extra" == *"RH_SHOT_REDUCED=1"* ]] && suffix="_reduced"
  out="production/qa/evidence/$dir/${state}_${sc}${suffix}.png"
  mkdir -p "production/qa/evidence/$dir"
  RH_ENV=(RH_SHOT="$WINDIR\\${out//\//\\}" RH_SHOT_MODE="$mode" RH_SHOT_UISCALE="$sc")
  if [ -n "$extra" ]; then IFS=';' read -ra kvs <<<"$extra"; for kv in "${kvs[@]}"; do RH_ENV+=("$kv"); done; fi
  rm -f "$out"
  timeout 120 bash -c '
    . tools/shellenv.sh || exit 1
    RH_ENV=("$@")
    dn run --project src/IdleXIdle.Game --no-build >"$SHOT_LOG" 2>&1
  ' _ "${RH_ENV[@]}"
  # A CAPTURE THAT PRODUCED NO FILE FAILED, and the log says why: a dial that does not parse throws
  # rather than photographing a plausible default (Game1.ParsePosedCursor, RH_SHOT_DISPATCH).
  if [ -f "$out" ]; then echo "ok   $out"; taken=$((taken+1));
  else echo "FAIL $out"; failed=$((failed+1)); grep -E 'Exception|RH_SHOT' "$SHOT_LOG" | tail -3 | sed 's/^/     /'; fi
done

# ── THE END OF THE OPENING, from the opening rig's own film ────────────────────────────────────
#
# The four tiles the authored opening walks past break TOGETHER on the frame the last card is
# answered — a frame no shutter can pose, because the shutter never plays an opening. The rig films
# a burst on that frame (Game1.cs, OpeningRigMark "NAV_BREAK"), so the picture is lifted from it.
if [ -z "$FILTER" ] || [[ "opening" == *"$FILTER"* ]]; then
  for sc in 100 150; do
    dir="build/shots/opening_flow/$sc"
    # A LANE THAT CANNOT FAIL IS NOT A LANE. The opening run is a row of this matrix like any other,
    # so its exit code counts: a dirty trace must not let the run end on "0 failed".
    if ! bash tools/check_opening_flow.sh "$sc"; then
      echo "FAIL the opening run at $sc % did not finish clean"; failed=$((failed+1))
    fi
    # THE MIDDLE OF THE BURST, not its first frame: the mark is filmed on the frame the break is
    # ARMED, where nothing has moved yet and four intact chains look exactly like four locked ones.
    frame="$(ls "$dir"/*break_*.png 2>/dev/null | awk -v n="$(ls "$dir"/*break_*.png 2>/dev/null | wc -l)" 'NR==int(n/2)+1')"
    if [ -n "$frame" ]; then
      cp "$frame" "production/qa/evidence/attention/flourish/openingend_breaks_${sc}.png"
      echo "ok   production/qa/evidence/attention/flourish/openingend_breaks_${sc}.png  <- $(basename "$frame")"
      taken=$((taken+1))
    else
      echo "FAIL no NAV_BREAK frame was filmed at $sc %"; failed=$((failed+1))
    fi
  done
fi

echo "attention fixtures: $taken captured, $failed failed"
# ...and the count is the exit code, so a caller that only reads $? learns the same thing a reader does.
[ "$failed" -eq 0 ] || exit 1
