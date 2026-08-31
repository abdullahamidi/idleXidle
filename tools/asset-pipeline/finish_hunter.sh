#!/bin/bash
# After the unarmed hunter clips regenerate: re-cut the statics from them and rebuild.
# hunter_idle / hunter_attack_01 / hunter_defeated are SLICED from these strips, so they
# must be re-derived or the screen keeps drawing the old armed poses.
. "$(dirname "${BASH_SOURCE[0]}")/../shellenv.sh" || exit 1
cd "$(dirname "${BASH_SOURCE[0]}")/../.." || exit 1
while proc_running "asset-pipeline.animate\.py"; do sleep 30; done
echo "[$(date +%H:%M)] strips done; re-deriving statics"
py -u tools/asset-pipeline/derive_statics.py 2>&1 | tail -5
py -u tools/asset-pipeline/audit.py 2>&1 | head -6
dn build src/ResonanceHunter.Game/ResonanceHunter.Game.csproj -v q --nologo 2>&1 | tail -4
echo "[$(date +%H:%M)] HUNTER FINALIZE DONE"
