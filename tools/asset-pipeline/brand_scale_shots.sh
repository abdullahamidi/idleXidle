#!/usr/bin/env bash
# tools/asset-pipeline/brand_scale_shots.sh -- BRAND size evidence (ADR-011, 2026-10-01): one single-shot fight per host with RH_SHOT_DUMP (the actor geometry), BRAND woven in.
cd "$(dirname "${BASH_SOURCE[0]}")/../.." || exit 1
O=build/shots/brand/scale
BASE="volley_spray:volley_spray@Body,hammer_press:sign_brand@Shadow"
shot() { local name=$1; shift
  [ -f "$O/$name.png.actors.txt" ] && return
  env "$@" RH_SHOT_SWAP="$BASE" RH_SHOT_SEED=7 RH_SHOT_HUNTER=seeker RH_SHOT_DUMP=1 RH_SHOT_T=2.6 \
    timeout 300 bash tools/asset-pipeline/capture.sh fight "$O/$name.png" >/dev/null 2>&1
  echo "$name $( [ -f "$O/$name.png.actors.txt" ] && echo ok || echo FAIL)"
}
shot default
for src in Nature Machine Shadow Body Mind Spirit; do
  for a in Swarm Caster Armoured Bruiser; do shot "${src}_${a}" RH_SHOT_SOURCE=$src RH_SHOT_ARCHETYPE=$a; done
done
for b in thorn_regent forge_colossus void_reaper spirit_matron crystal_lich lumen_angel; do shot "boss_$b" RH_SHOT_BOSS=$b; done
