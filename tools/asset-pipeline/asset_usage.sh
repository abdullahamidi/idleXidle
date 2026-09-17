#!/bin/bash
# WHICH TEXTURES DOES THE GAME ACTUALLY ASK FOR? The capture battery, with AssetLibrary's request trace on.
#
#   bash tools/asset-pipeline/asset_usage.sh           # the whole battery (~40-60 min), then the report
#   bash tools/asset-pipeline/asset_usage.sh --report  # only the report, from the last trace
#   bash tools/asset-pipeline/asset_usage.sh --fixtures  # keep the trace, run only the fixture scripts
#                                                        # (resume a battery stopped after the rig captures)
#
# WHY. A texture's use has no static answer in this project: keys are built at runtime from the roster,
# the regions, the skill catalogue and the clip names, and tools/check_asset_consumers.py counts a whole
# interpolated family as reached (every fx_* file is "used" because `fx_` heads an interpolated key, which
# is how fx_levelup sat unplayed and eagerly loaded for weeks). RH_ASSET_TRACE makes AssetLibrary append
# every key it is ASKED for — Get, Has, GetFirst, WhiteMask, an alias target, a Warm prefix's matches —
# so running every fixture the rig can pose gives the set some screen really requested.
#
# WHAT IT DOES NOT PROVE. A key the battery never asked for is a CANDIDATE, not a verdict: a boss the
# fixtures never visit, a clip only a rare beat plays. Every candidate still needs its family traced in
# the code before it is deleted (the 2026-09-17 removal did that per family; see
# production/qa/asset-usage/).
#
# SIDE EFFECTS. Several fixture scripts write into production/qa/evidence/. The battery snapshots that
# tree first and restores it at the end, so a usage run never rewrites committed evidence.
set -u
. "$(dirname "${BASH_SOURCE[0]}")/../shellenv.sh" || exit 1
cd "$(dirname "${BASH_SOURCE[0]}")/../.." || exit 1
OUT="build/asset_usage"
TRACE="$OUT/requested.txt"
mkdir -p "$OUT"

MODE="${1:-}"
if [ "$MODE" != "--report" ]; then
  [ "$MODE" = "--fixtures" ] || : > "$TRACE"
  export RH_ASSET_TRACE="$(winpath "$PWD")\\build\\asset_usage\\requested.txt"
  # Evidence snapshot: tracked files are restored with git; files the fixtures ADD are listed and removed.
  git ls-files --others --exclude-standard -- production/qa/evidence > "$OUT/evidence_untracked_before.txt"

  MODES=(title vfx forge farm dust world region2 region3 conquered mapdeep maplocked help expedition fight
         fightshield welcome boss bossdebug banked lootforge settings settingsfull settingsopen vow runlog
         reforge buildtree buildzoom character itemmenu stats trainingpoor trainingreset warren map rig
         corrupted corruptedboss fightgear fightswing fightreport fightfall fightfade fightarrive
         fightcooldown fightaura fightflash fightstatus fightfive fightshieldbroken fightmulti fightinspect
         roster rosterlocked rosterswitch warrenready warrenfresh warrenlocked weave weavefresh vault
         vaultfirst vaultfilter attune attuned trader dispatches dispatchesempty dispatchesunread
         dispatcheshover chestdispatch vaultempty vaultemptyfilter vaultsell vaultmany forgeempty gemtour
         intro typespec vfxdebug)
  SPECS=()
  for m in "${MODES[@]}"; do SPECS+=("$m:100"); done
  for a in Hunt Training Gear Vault Forge Build Mastery Map Warren Traits Roster; do SPECS+=("tour:100:RH_SHOT_TAB=$a"); done
  for t in reroll socket breakdown; do SPECS+=("forge:100:RH_SHOT_TAB=$t"); done
  for c in seeker anvil chorus metronome unbroken tower quiver oathbound thornwall magpie; do
    for m in fight fightswing fightaura fightfall fightarrive roster buildtree weave; do SPECS+=("$m:100:RH_SHOT_HUNTER=$c"); done
  done
  for m in fight settings buildtree forge warren; do SPECS+=("$m:150"); done

  if [ "$MODE" != "--fixtures" ]; then
    echo "== battery: ${#SPECS[@]} rig captures"
    bash tools/asset-pipeline/polish_shots.sh usage "${SPECS[@]}" | grep -c '^ok' | sed 's/^/   ok: /'
  fi
  for s in enemy_fixtures vfx_shots prologue_fixtures item_fixtures attention_fixtures warren_fixtures \
           hunt_geometry_fixtures focus_fixtures integration_fixtures; do
    echo "== $s"
    # warren_fixtures and hunt_geometry_fixtures take an output tag; the others take an optional filter.
    arg=(); case "$s" in warren_fixtures|hunt_geometry_fixtures) arg=(usage) ;; esac
    bash "tools/asset-pipeline/$s.sh" ${arg[@]+"${arg[@]}"} >"$OUT/$s.log" 2>&1 || echo "   (exited non-zero; see $OUT/$s.log)"
  done
  echo "== check_boot";         bash tools/check_boot.sh >"$OUT/check_boot.log" 2>&1 | true; tail -1 "$OUT/check_boot.log"
  echo "== check_opening_flow"; bash tools/check_opening_flow.sh 100 >"$OUT/opening.log" 2>&1 | true; tail -1 "$OUT/opening.log"

  # Restore the evidence tree.
  git checkout -- production/qa/evidence
  git ls-files --others --exclude-standard -- production/qa/evidence > "$OUT/evidence_untracked_after.txt"
  comm -13 <(sort "$OUT/evidence_untracked_before.txt") <(sort "$OUT/evidence_untracked_after.txt") \
    | while IFS= read -r f; do [ -f "$f" ] && rm -f -- "$f"; done
  unset RH_ASSET_TRACE
fi

py tools/asset_usage_report.py "$TRACE"
