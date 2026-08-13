#!/bin/bash
# Launch the game from a WSL shell.
#
#   bash tools/run.sh            # build, then play
#   bash tools/run.sh --no-build # skip the build (only if you just built)
#
# `dotnet run --project src/ResonanceHunter.Game` fails from WSL with "command not
# found", because there is no Linux .NET SDK in this distro — the toolchain is the
# WINDOWS one at /mnt/c/Program Files/dotnet. It also could not open a window from
# here if it did exist. So the invocation is handed to cmd.exe, exactly as
# tools/asset-pipeline/capture.sh does.
#
# From a Windows shell (PowerShell / cmd) the plain command works and you do not
# need this script.
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")/.." || exit 1
WINDIR="$(wslpath -w "$PWD")"

if [ "${1:-}" != "--no-build" ]; then
  cmd.exe /c "cd /d $WINDIR && dotnet build src\\ResonanceHunter.Game -v q --nologo" \
    || { echo "build failed" >&2; exit 1; }
fi

# No RH_SHOT: this is a real session. It reads and WRITES the player's save, which is
# why the capture rig deliberately never does (see Game1.LoadOrStartFresh).
exec cmd.exe /c "cd /d $WINDIR && dotnet run --project src\\ResonanceHunter.Game --no-build"
