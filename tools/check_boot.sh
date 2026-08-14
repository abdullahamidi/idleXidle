#!/bin/bash
# Does the game actually START — with a real save loaded?
#
#   bash tools/check_boot.sh
#
# NOT part of check_all.sh, which is the build-free set. This one builds and runs the game, so it
# costs ~15 seconds; run it before shipping anything that touches startup, saves or persistence.
#
# WHY IT EXISTS. `LoadOrStartFresh` returns immediately when RH_SHOT is set — deliberately, so a
# screenshot can never entangle with or write back real progress. The consequence is that the
# save-load path is the one path no capture can reach, and the game once shipped a startup crash
# that lived there: `_forge` is built in LoadContent, `LoadOrStartFresh` runs from Initialize, and
# dereferencing it threw before the window opened. A first launch has no save and returns early, so
# the crash only began on the SECOND launch. Every gate and every screenshot was green.
#
# RH_BOOTCHECK is the mode that closes it: it loads the REAL save, runs 90 frames of the real
# Update/Draw loop, prints what it restored and exits 0. It cannot write — it shares the single
# guard in Game1.Save() with RH_SHOT — and this script checks that claim rather than trusting it.
set -uo pipefail
. "$(dirname "${BASH_SOURCE[0]}")/shellenv.sh" || exit 1
cd "$(dirname "${BASH_SOURCE[0]}")/.." || exit 1

SAVE="${LOCALAPPDATA:-$HOME/.local/share}/ResonanceHunter/save.json"

dn build src/ResonanceHunter.Game -v q --nologo >/dev/null 2>&1 \
  || { echo "build failed" >&2; exit 1; }

before=""
[ -f "$SAVE" ] && before="$(sha256sum "$SAVE" | cut -d' ' -f1)"
[ -n "$before" ] || echo "note: no save file at $SAVE — this run proves the FRESH path only."

RH_ENV=(RH_BOOTCHECK=1)
out="$(dn run --project src/ResonanceHunter.Game --no-build 2>&1)"
rc=$?

echo "$out" | grep -aE "BOOT OK|Unhandled|Exception" | head -5

if [ $rc -ne 0 ] || ! echo "$out" | grep -aq "BOOT OK"; then
  echo "BOOT FAILED — the game does not start." >&2
  echo "$out" | tail -20 >&2
  exit 1
fi

# The other half of the claim: a mode that reads the player's save must not have written one.
if [ -n "$before" ]; then
  after="$(sha256sum "$SAVE" | cut -d' ' -f1)"
  if [ "$before" != "$after" ]; then
    echo "BOOT CHECK WROTE THE PLAYER'S SAVE — the guard in Game1.Save() is not holding." >&2
    exit 1
  fi
  echo "save untouched."
fi

echo "boot green"
