#!/bin/bash
# Re-take the VFX placement contract's whole evidence set (brief 61-73, 105, 106, 112).
#
#   bash tools/asset-pipeline/vfx_shots.sh
#
# Six fight modes at the three density profiles, plus the debug view on the two silhouettes the
# brief names (SEEKER, MAGPIE) and the two the audit proved are the pair that can actually fail
# (OATHBOUND 162 px wide, QUIVER 373). Written as a script rather than typed each time so the
# evidence set is reproducible and every shot is taken from the same build.
set -u
cd "$(dirname "$0")/../.." || exit 1
mkdir -p build/shots

for m in fight fightshield fightshieldbroken fightstatus fightmulti boss; do
  for s in 100 125 150; do
    echo "-- $m @ $s"
    RH_SHOT_UISCALE=$s bash tools/asset-pipeline/capture.sh "$m" "build/shots/vfx_${m}_${s}.png" || exit 1
  done
done

for h in seeker magpie quiver oathbound; do
  echo "-- vfxdebug @ $h"
  RH_SHOT_HUNTER=$h RH_VFX_DUMP=1 bash tools/asset-pipeline/vfx_dump.sh vfxdebug "build/shots/vfx_debug_${h}.png" || exit 1
  RH_SHOT_HUNTER=$h bash tools/asset-pipeline/capture.sh fightshield "build/shots/vfx_shield_${h}.png" || exit 1
done

for s in 125 150; do
  echo "-- vfxdebug seeker @ $s"
  RH_SHOT_UISCALE=$s RH_SHOT_HUNTER=seeker bash tools/asset-pipeline/capture.sh vfxdebug "build/shots/vfx_debug_seeker_${s}.png" || exit 1
done
RH_SHOT_HUNTER=seeker bash tools/asset-pipeline/capture.sh vfxdebug build/shots/vfx_debug_seeker_100.png || exit 1

RH_VFX_BUDGET=1 bash tools/asset-pipeline/vfx_dump.sh fightshield build/shots/vfx_ledger.png || exit 1
echo "vfx evidence set rebuilt under build/shots/"
