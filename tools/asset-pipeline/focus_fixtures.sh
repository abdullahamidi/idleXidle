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
  # THE ROWS ARE LIT BY THE OPENING NOW, not by the contextual lesson. TRAIN ANY STAT became an
  # authored beat (OpeningStage.ForceTrainStat) and FirstTrainingPurchase was demoted to the quiet
  # slot with it, so posing the LESSON here photographs a screen with no light on it at all.
  "training_rows|stats|RH_SHOT_OPENING=ForceTrainStat|"

  # ── PRESENTATION ONLY: the same lit control, explained and then forced. ──────────────────────
  #
  # The pair that proves the rule a screenshot of either frame alone cannot: the cursor is parked
  # dead centre on the VAULT rail tile (canvas 90,539) in both, and the tile carries its gold hover
  # wash ONLY in the frame where pressing it would do something. On the explanation it is lit and
  # inert — which is the whole point, because a highlighted control that lights up under the mouse
  # looks actionable whether or not it is.
  "vault_tile_explained|fight|RH_SHOT_OPENING=IntroduceChest RH_SHOT_CANVAS_MOUSE=90,539|"
  "vault_tile_forced|fight|RH_SHOT_OPENING=ForceVault RH_SHOT_CANVAS_MOUSE=90,539|"

  # ── DEMOTED: what a lesson looks like once it stops darkening the page. ─────────────────────
  #
  # Not a light at all, and that is the state worth photographing: the lesson keeps its title, its
  # line and its x in the screen's own quiet slot, with the page readable and live underneath. It
  # is also the state that was silent until the attention owner stopped claiming the frame for a
  # light it had stopped painting (lesson_demotion_test).
  "demoted_slot_training|stats|RH_SHOT_LESSON=FirstTrainingPurchase|"
  "demoted_slot_mastery|buildtree|RH_SHOT_LESSON=FirstMasterySpend|"
)

fail=0
for row in "${ROWS[@]}"; do
  IFS='|' read -r state mode dials arg <<<"$row"
  for scale in 100 125 150; do
    name="${state}_${scale}"
    case "$name" in *"$FILTER"*) ;; *) continue ;; esac
    echo "── $name  ($mode $arg, $dials)"
    if ! env RH_SHOT_UISCALE="$scale" $dials bash tools/asset-pipeline/capture.sh "$mode" "$OUT/$name.png" $arg >/dev/null 2>&1; then
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
