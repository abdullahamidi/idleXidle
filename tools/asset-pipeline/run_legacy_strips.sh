#!/bin/bash
# Regenerate ONLY the strips still holding legacy painted art, most
# player-visible first, then verify.
#
# Everything dated 2026-07-24 in assets/art is from the old painted library;
# anything dated 2026-08-10 is already the new pixel style. Re-running --force
# over all 46 wasted generations redoing strips that were already converted.
#
# Order matters: if this is interrupted, the tiers that got done are the ones
# the player looks at most.

. "$(dirname "${BASH_SOURCE[0]}")/../shellenv.sh" || exit 1
cd "$(dirname "${BASH_SOURCE[0]}")/../.." || exit 1
LOG=tools/asset-pipeline/.staging
stamp() { date -u +%H:%M:%S; }

BOSSES="thorn_regent forge_colossus void_reaper crystal_lich lumen_angel spirit_matron"
HUNTER="hunter_idle hunter_attack hunter_cast hunter_hurt_recover hunter_death"
VFX="aura_arcane_ring binding_void_bind heal_holy_burst impact_gold levelup_gold_purple loot_pop projectile_arcane slash_nature slash_void smoke_puff"

# Tier 1 — bosses. Biggest thing on the dominant screen.
ARGS=""
for b in $BOSSES; do
  ARGS="$ARGS --only ${b}_idle_strip8_1024 --only ${b}_attack_strip8_1024"
done
echo "[$(stamp)] TIER 1: 12 boss strips"
py -u tools/asset-pipeline/animate.py --force --workers 1 $ARGS 2>&1 | tail -20

# Tier 2 — the player character and the one enemy still on old art.
ARGS=""
for h in $HUNTER; do ARGS="$ARGS --only ${h}_strip8_512"; done
ARGS="$ARGS --only stone_sentinel_idle_strip8_512 --only stone_sentinel_slam_strip8_512"
echo "[$(stamp)] TIER 2: hunter + stone_sentinel (7 strips)"
py -u tools/asset-pipeline/animate.py --force --workers 1 $ARGS 2>&1 | tail -20

# Tier 3 — remaining legacy VFX.
ARGS=""
for v in $VFX; do ARGS="$ARGS --only ${v}_strip8_512"; done
echo "[$(stamp)] TIER 3: 10 legacy VFX strips"
py -u tools/asset-pipeline/animate.py --force --workers 1 $ARGS 2>&1 | tail -20

echo "[$(stamp)] TIER 4: derive statics from strips"
py -u tools/asset-pipeline/derive_statics.py 2>&1 | tail -20

echo "[$(stamp)] ALL TIERS DONE"
