#!/bin/bash
# Publish the self-contained Windows build and push it to itch.io with butler.
#
#   bash tools/publish/itch_push.sh <itch-user>/<game-slug> [channel]
#
# Needs BUTLER_API_KEY in the environment (itch.io → Settings → API keys; set it with
# `setx BUTLER_API_KEY "<key>"` and open a NEW terminal — setx does not reach a running shell).
# butler lives at %LOCALAPPDATA%\butler\butler.exe (installed 2026-08-25 from broth.itch.zone);
# the script installs it there if it is missing.
#
# What it does, in order: `dotnet publish` (Release, win-x64, self-contained) into
# build/release/IDLExIDLE-win64, a smoke boot of the published exe under RH_SHOT (a build that
# cannot draw its title is not uploaded), then `butler push` of the FOLDER (butler diffs and
# uploads only what changed — a 65 MB zip becomes a few MB after the first push), stamped with
# the git sha as the user-facing version so a tester's feedback code and the build page agree.
set -euo pipefail
TARGET="${1:?usage: itch_push.sh <user>/<slug> [channel]}"
CHANNEL="${2:-windows}"
. "$(dirname "${BASH_SOURCE[0]}")/../shellenv.sh" || exit 1
cd "$(dirname "${BASH_SOURCE[0]}")/../.." || exit 1

BUTLER="${LOCALAPPDATA}/butler/butler.exe"
if [ ! -x "$BUTLER" ]; then
  echo "installing butler…"
  mkdir -p "${LOCALAPPDATA}/butler" && cd "${LOCALAPPDATA}/butler" \
    && curl -sL "https://broth.itch.zone/butler/windows-amd64/LATEST/archive/default" -o butler.zip \
    && python -c "import zipfile; zipfile.ZipFile('butler.zip').extractall('.')" \
    && cd - >/dev/null
fi
[ -n "${BUTLER_API_KEY:-}" ] || { echo "BUTLER_API_KEY is not set in this shell" >&2; exit 1; }

SHA="$(git rev-parse --short HEAD)"
OUT="build/release/IDLExIDLE-win64"
echo "== publish ${SHA} → ${OUT}"
dn publish src/ResonanceHunter.Game -c Release -r win-x64 --self-contained -o "$OUT" -v q --nologo \
  || { echo "publish failed" >&2; exit 1; }

echo "== smoke boot of the published exe"
SHOT="$(winpath "$PWD")\\build\\release\\smoke_${SHA}.png"
( cd "$OUT" && RH_SHOT="$SHOT" RH_SHOT_MODE=title ./IDLExIDLE.exe >/dev/null 2>&1 ) || true
[ -f "build/release/smoke_${SHA}.png" ] || { echo "the published build did not draw its title — not uploading" >&2; exit 1; }
rm -f "build/release/smoke_${SHA}.png"

echo "== butler push ${TARGET}:${CHANNEL} (userversion ${SHA})"
"$BUTLER" push "$OUT" "${TARGET}:${CHANNEL}" --userversion "$SHA" --if-changed
"$BUTLER" status "${TARGET}:${CHANNEL}" | tail -5
