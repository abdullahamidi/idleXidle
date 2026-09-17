#!/bin/bash
# The focus light, photographed: every lit shape family at 100 / 125 / 150, plus one filmstrip.
#
#   bash tools/asset-pipeline/focus_fixtures.sh [filter]
#
# One row per capture: <state>|<mode>|<scale>|<dials>. The filter is a substring of "<state>_<scale>",
# so `hunter` retakes the three hunter frames and `_100` retakes one profile. Everything lands in
# production/qa/evidence/visual-pass/focus/<state>_<scale>.png; the README there is the verdict.
. "$(dirname "${BASH_SOURCE[0]}")/../shellenv.sh" || exit 1
cd "$(dirname "${BASH_SOURCE[0]}")/../.." || exit 1
FILTER="${1:-}"
OUT=production/qa/evidence/visual-pass/focus
mkdir -p "$OUT"

ROWS=(
  # ACTOR lights: the figure's own silhouette.
  "hunter_intro|fight|RH_SHOT_OPENING=IntroduceHunter|"
  "enemy_intro|fight|RH_SHOT_OPENING=IntroduceEnemy|"
  "boss_intro|boss|RH_SHOT_OPENING=IntroduceBoss|"
  # The posed IntroduceEnemy beat parks the fight before its first wave has art (the fixture's
  # creature is the ember block), so the PACK's silhouettes are photographed on the HUNT tour's
  # own ENEMIES card, over the running fixture.
  "enemy_tour|intro||2"
  # UI lights: a soft plate per rectangle.
  "health_panel|fight|RH_SHOT_OPENING=IntroduceHealth|"
  "stage_header|fight|RH_SHOT_OPENING=IntroduceStage|"
  "vault_tile|fight|RH_SHOT_OPENING=IntroduceChest|"
  "chest|vaultfirst|RH_SHOT_OPENING=ForceChestOpen|"
  "gear_cell|character|RH_SHOT_OPENING=ForceItemSelect|"
  "item_detail|character|RH_SHOT_OPENING=ExplainItem|"
  "training_rows|stats|RH_SHOT_LESSON=FirstTrainingPurchase|"
  "mastery_target|buildtree|RH_SHOT_LESSON=FirstMasterySpend|"
)

fail=0
for row in "${ROWS[@]}"; do
  IFS='|' read -r state mode dials arg <<<"$row"
  for scale in 100 125 150; do
    name="${state}_${scale}"
    case "$name" in *"$FILTER"*) ;; *) continue ;; esac
    echo "── $name  ($mode $arg, $dials)"
    if ! env RH_SHOT_UISCALE="$scale" ${dials:+"$dials"} bash tools/asset-pipeline/capture.sh "$mode" "$OUT/$name.png" $arg >/dev/null 2>&1; then
      echo "   FAILED"; fail=1
    fi
  done
done

# THE FILMSTRIP: the live hunter beat over sixteen frames, six apart — the blaze holding, the figure
# animating under a light that follows the pose.
case "filmstrip_hunter_100" in *"$FILTER"*)
  echo "── filmstrip_hunter_100"
  RH_SHOT_OPENING=IntroduceHunter RH_SHOT_UISCALE=100 bash tools/asset-pipeline/capture_seq.sh fight 16 6 "$OUT/filmstrip_hunter_100" >/dev/null 2>&1 || { echo "   FAILED"; fail=1; }
  rm -f "$OUT"/filmstrip_hunter_100_[0-9][0-9].png
  [ -f "$OUT/filmstrip_hunter_100_strip.png" ] && mv -f "$OUT/filmstrip_hunter_100_strip.png" "$OUT/filmstrip_hunter_100.png"
  ;;
esac

[ $fail -eq 0 ] && echo "focus fixtures done" || echo "FOCUS FIXTURES: some captures failed"
exit $fail
