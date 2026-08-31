#!/bin/bash
# Launch the game.
#
#   bash tools/run.sh            # build, then play
#   bash tools/run.sh --no-build # skip the build (only if you just built)
#
# From WSL, `dotnet run` fails with "command not found" — there is no Linux .NET SDK in
# that distro, the toolchain is the WINDOWS one, and it could not open a window from there
# even if it existed. So the call is handed to cmd.exe. From Git Bash or a Linux checkout
# the direct call works and is used instead. tools/shellenv.sh picks between them.
set -euo pipefail
. "$(dirname "${BASH_SOURCE[0]}")/shellenv.sh" || exit 1
cd "$(dirname "${BASH_SOURCE[0]}")/.." || exit 1

if [ "${1:-}" != "--no-build" ]; then
  dn build src/ResonanceHunter.Game -v q --nologo \
    || { echo "build failed" >&2; exit 1; }
fi

# No RH_SHOT: this is a real session. It reads and WRITES the player's save, which is
# why the capture rig deliberately never does (see Game1.LoadOrStartFresh).
dn run --project src/ResonanceHunter.Game --no-build
