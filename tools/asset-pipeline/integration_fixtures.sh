#!/bin/bash
# One surface at a time, photographed: the pairs the 2026-09-16 visual pass could have set against each
# other, each posed with both claims live, so the picture shows which one owns the frame (ADR-007).
#
#   bash tools/asset-pipeline/integration_fixtures.sh [filter]
#
# Rows are <name>|<mode>|<scale>|<dials>|<third argument>; files land in
# production/qa/evidence/visual-pass/integration/<name>_<scale>.png. The README there is the verdict.
#
#   chest_letter_during   a letter posted DURING the first chest reveal: nothing pulses, nothing toasts
#   chest_letter_after    the same, past the reveal's hold: the envelope carries the letter
#   reveal_notice_during  a notice queued under a chest reveal: it does not paint and its clock holds
#   reveal_notice_after   past the hold: the notice lands
#   enemy_intro_notice    a notice queued under the opening's enemy introduction: the light is alone
#   fight_lesson          the control: the coach's first HUNT lesson lighting the Hunter on a live fight
#   fade_lesson           the same lesson posed during the death transition (0.1, the collapse): the
#                         death owns the frame, so no light, no card
#   fade_lesson_black     the same at 0.5, the black with the next descent beginning beneath it
#   gear_lesson           the Gear lesson lighting a cell of the enlarged item frame
#   letters_panel         the DISPATCHES panel open over a fight (the panel's scrim owns the page)
#   dispatch_lesson       the first-letter lesson, once nothing above the coach owns the frame: the
#                         envelope lit, the letter's mark on
. "$(dirname "${BASH_SOURCE[0]}")/../shellenv.sh" || exit 1
cd "$(dirname "${BASH_SOURCE[0]}")/../.." || exit 1
FILTER="${1:-}"
OUT=production/qa/evidence/visual-pass/integration
mkdir -p "$OUT"

ROWS=(
  "chest_letter_during|chestdispatch|RH_SHOT_DISPATCH=unread|1.2"
  "chest_letter_after|chestdispatch|RH_SHOT_DISPATCH=unread|5"
  "reveal_notice_during|lootforge|RH_SHOT_NOTICE=1|1.2"
  "reveal_notice_after|lootforge|RH_SHOT_NOTICE=1|5"
  "enemy_intro_notice|fight|RH_SHOT_OPENING=IntroduceEnemy RH_SHOT_NOTICE=1|"
  "fight_lesson|fight|RH_SHOT_LESSON=FirstFight|"
  "fade_lesson|fightfade|RH_SHOT_LESSON=FirstFight|0.1"
  "fade_lesson_black|fightfade|RH_SHOT_LESSON=FirstFight|0.5"
  "gear_lesson|character|RH_SHOT_OPENING=ForceItemSelect|"
  "letters_panel|dispatches|RH_SHOT_DISPATCH=unread|"
  "dispatch_lesson|dispatchesunread|RH_SHOT_DISPATCH=unread RH_SHOT_LESSON=FirstDispatchOpened|"
)

fail=0
for row in "${ROWS[@]}"; do
  IFS='|' read -r name mode dials arg <<<"$row"
  for scale in 100 125 150; do
    tag="${name}_${scale}"
    case "$tag" in *"$FILTER"*) ;; *) continue ;; esac
    echo "── $tag  ($mode $arg, $dials)"
    # shellcheck disable=SC2086
    if ! env RH_SHOT_UISCALE="$scale" $dials bash tools/asset-pipeline/capture.sh "$mode" "$OUT/$tag.png" $arg >/dev/null 2>&1; then
      echo "   FAILED"; fail=1
    fi
    [ -f "$OUT/$tag.png" ] || { echo "   NO FILE"; fail=1; }
  done
done
[ $fail -eq 0 ] && echo "integration fixtures done" || echo "INTEGRATION FIXTURES: some captures failed"
exit $fail
