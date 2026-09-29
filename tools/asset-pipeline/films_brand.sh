#!/usr/bin/env bash
# tools/asset-pipeline/films_brand.sh <tag> [takes...] -- BRAND, the MARK / PERSISTENT TARGET-ATTACHED STATE reference
# (ADR-011, 2026-09-29): the Seeker's Etched Shadow Brand on the front enemy, filmed from the seeded fight (the same build
# as the JAWS and PRESS films, BRAND woven into a field slot).
#   <tag>/apply    every frame ~1.6-2.8 s (the first shot lands ~1.1 s after the seek): the wave's first tick, the coil gathering and cut ON the tick
#   <tag>/long     one picture per 2 frames, ~1.6-12.6 s: apply, idle, the front falling twice (the mark migrating)
#   <tag>/etch     `long` as ETCH: the mark deepening on every tick (120 / 170 / 220 / 240 %)
#   <tag>/sprawl   `long` as SPRAWL: the mark on every creature at half strength, spread from the front
#   <tag>/press    `long` with BRAND in JAWS's slot, PRESS kept: the crush and the brand on one tick and one body
#   <tag>/fast     fast TEMPO (the second segment), every frame 0.8-4.8 s: SPRAY and HARD HANDS on the branded body
#   <tag>/before   `long` with RH_MARK_RECIPES=0: BRAND as it was (the reticle held behind the hunter)
#   <tag>/etch_clean `etch` with WILT in JAWS's slot: the deepen ticks unobstructed by a bite reaction
#   <tag>/order    BRAND in PRESS's slot and PRESS in JAWS's: both presented (the trace shows field-wave and mark-wave)
#   <tag>/light    `long` on the Pale Choir's pale creatures: the ink half of the material
#   <tag>/apply_off, etch_tick (every frame ~3.6-4.8 s, ETCH's tick at 4000), etch_tick_off: the light measure's pairs
#   <tag>/clean_tick(_off), clean_tick6(_off): the same pairs on etch_clean's full-strength ticks at 4000 and 6000
#   <tag>/light_etch `light` as ETCH: the deep stages on the Pale Choir's pale creatures
#   <tag>/sprawl_winnow  `sprawl` with WINNOW (+10 % a tick): every coil deepens, each at its own place in the spread;
#                        sprawl_winnow_tick / _off: every frame round its 4000 tick, with and without the mark
#   <tag>/sprawl_winnow_clean (+ _tick / _tick_off)  the same with WILT in JAWS's slot: the ripple at full strength
#   <tag>/fam_<source>_<archetype>  BRAND on another region's creature (the authored body point on other families)
cd "$(dirname "${BASH_SOURCE[0]}")/../.." || exit 1
TAG=${1:-current}; shift
OUT=build/shots/brand/$TAG
mkdir -p "$OUT"
BASE="volley_spray:volley_spray@Body,hammer_press:sign_brand@Shadow"
WITH_PRESS="volley_spray:volley_spray@Body,snare_jaws:sign_brand@Shadow"
TEMPO="bite,swift,quick,road_sign_2,brisk,rhythm,blitz,volley"
film() {  # name swap T count stride start [ENV=VAL ...]
  local name=$1 swap=$2 t=$3 count=$4 stride=$5 start=$6; shift 6
  env RH_SHOT_SWAP="$swap" RH_SHOT_SEED=7 "$@" RH_PRESENT_TRACE=1 RH_SHOT_HUNTER=seeker RH_SHOT_T="$t" \
    RH_SEQ_LOG="$PWD/$OUT/$name.log" timeout 2400 bash tools/asset-pipeline/capture_seq.sh fight "$count" "$stride" "$OUT/$name" "$start" 2>&1 | tail -1
}
want() { [ ${#ARGS[@]} -eq 0 ] || [[ " ${ARGS[*]} " == *" $1 "* ]]; }
ARGS=("$@")
want apply   && film apply  "$BASE" 0.5 72 1 0
want long    && film long   "$BASE" 0.5 330 2 0
want etch    && film etch   "$BASE" 0.5 330 2 0 RH_SHOT_VARIATION=sign_brand:ETCH
want sprawl  && film sprawl "$BASE" 0.5 330 2 0 RH_SHOT_VARIATION=sign_brand:SPRAWL
want press   && film press  "$WITH_PRESS" 0.5 330 2 0
want fast    && film fast   "$BASE" 3.4 240 1 732 RH_SHOT_TAKE="$TEMPO"
want before  && film before "$BASE" 0.5 330 2 0 RH_MARK_RECIPES=0
# A CLEAN DEEPEN: ETCH with JAWS swapped for WILT (no bite reaction over the host), so its ticks cut unobstructed
want etch_clean && film etch_clean "$BASE,snare_jaws:drain_wilt@Shadow" 0.5 330 2 0 RH_SHOT_VARIATION=sign_brand:ETCH
# BRAND WOVEN BEFORE PRESS (the order the first Field used to lose): both must be presented, the field and the mark
want order   && film order  "hammer_press:sign_brand@Shadow,snare_jaws:hammer_press@Body" 0.5 150 2 0
# A LIGHT BODY (the Pale Choir's): the brand's ink half, the char that reads where the lit smoke cannot
want light   && film light  "$BASE" 0.5 330 2 0 RH_SHOT_SOURCE=Spirit
# THE LIGHT MEASURE'S PAIRS: every frame (60 Hz, so a pair aligns frame for frame), with and without the mark
want apply_off && film apply_off "$BASE" 0.5 72 1 0 RH_MARK_RECIPES=0
want etch_tick && film etch_tick "$BASE" 2.5 72 1 0 RH_SHOT_VARIATION=sign_brand:ETCH
want etch_tick_off && film etch_tick_off "$BASE" 2.5 72 1 0 RH_SHOT_VARIATION=sign_brand:ETCH RH_MARK_RECIPES=0
# THE CLEAN DEEPEN'S PAIRS: ETCH with WILT in JAWS's slot, every frame, with and without the mark: the 4000 tick
# (120 -> 170 %, ~3.6-4.8 s) and the 6000 tick (170 -> 220 %, the second host, ~5.6-6.8 s), both full strength
CLEAN="$BASE,snare_jaws:drain_wilt@Shadow"
want clean_tick && film clean_tick "$CLEAN" 2.5 72 1 0 RH_SHOT_VARIATION=sign_brand:ETCH
want clean_tick_off && film clean_tick_off "$CLEAN" 2.5 72 1 0 RH_SHOT_VARIATION=sign_brand:ETCH RH_MARK_RECIPES=0
want clean_tick6 && film clean_tick6 "$CLEAN" 4.5 72 1 0 RH_SHOT_VARIATION=sign_brand:ETCH
want clean_tick6_off && film clean_tick6_off "$CLEAN" 4.5 72 1 0 RH_SHOT_VARIATION=sign_brand:ETCH RH_MARK_RECIPES=0
# ETCH ON A LIGHT BODY (the Pale Choir's): the deep stages' ink on a pale creature
want light_etch && film light_etch "$BASE" 0.5 330 2 0 RH_SHOT_SOURCE=Spirit RH_SHOT_VARIATION=sign_brand:ETCH
# SPRAWL + WINNOW: a deepen on EVERY coil of the row (it must ripple, never flash the row)
want sprawl_winnow && film sprawl_winnow "$BASE" 0.5 330 2 0 RH_SHOT_VARIATION=sign_brand:SPRAWL+WINNOW
want sprawl_winnow_tick && film sprawl_winnow_tick "$BASE" 2.5 72 1 0 RH_SHOT_VARIATION=sign_brand:SPRAWL+WINNOW
want sprawl_winnow_tick_off && film sprawl_winnow_tick_off "$BASE" 2.5 72 1 0 RH_SHOT_VARIATION=sign_brand:SPRAWL+WINNOW RH_MARK_RECIPES=0
# SPRAWL + WINNOW AT FULL STRENGTH: WILT in JAWS's slot, so no bite reaction quiets or covers the row's ripple
want sprawl_winnow_clean && film sprawl_winnow_clean "$CLEAN" 0.5 330 2 0 RH_SHOT_VARIATION=sign_brand:SPRAWL+WINNOW
want sprawl_winnow_clean_tick && film sprawl_winnow_clean_tick "$CLEAN" 2.5 72 1 0 RH_SHOT_VARIATION=sign_brand:SPRAWL+WINNOW
want sprawl_winnow_clean_tick_off && film sprawl_winnow_clean_tick_off "$CLEAN" 2.5 72 1 0 RH_SHOT_VARIATION=sign_brand:SPRAWL+WINNOW RH_MARK_RECIPES=0
# OTHER FAMILIES: the authored body point on creatures the reference fight never shows
for fam in Machine:Armoured Shadow:Caster Mind:Swarm Nature:Bruiser; do
  src=${fam%%:*}; arch=${fam##*:}; name="fam_$(echo "$src" | tr 'A-Z' 'a-z')_$(echo "$arch" | tr 'A-Z' 'a-z')"
  want "$name" && film "$name" "$BASE" 0.5 150 2 0 RH_SHOT_SOURCE="$src" RH_SHOT_ARCHETYPE="$arch" RH_SHOT_VARIATION=sign_brand:ETCH
done
exit 0
