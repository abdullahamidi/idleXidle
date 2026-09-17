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
# What it does, in order: CLEAN build/release/IDLExIDLE-win64 (butler pushes the folder, so anything
# an older build left behind would ship), `dotnet publish` (Release, win-x64, self-contained) into
# it, copy docs/publisher-notes.md in as README.md, a smoke boot of the published exe under RH_SHOT
# (a build that cannot draw its title is not uploaded), then `butler push` of the FOLDER (butler diffs and
# uploads only what changed — a 65 MB zip becomes a few MB after the first push), stamped with
# "<Version>+<git sha>" as the user-facing version — the same stamp the game shows as BUILD and a
# tester's feedback code carries, so the report and the build page agree. <Version> is the release
# label in Directory.Build.props (0.1.0-alpha since 2026-09-17; the pre-alpha pushes were the bare sha).
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

SHA="$(git rev-parse --short=8 HEAD)"   # 8, as BuildStamp.Short prints it
VERSION="$(grep -o '<Version>[^<]*' Directory.Build.props | head -1 | cut -d'>' -f2)"
[ -n "$VERSION" ] || { echo "no <Version> in Directory.Build.props" >&2; exit 1; }
USERVERSION="${VERSION}+${SHA}"
OUT="build/release/IDLExIDLE-win64"

# A CLEAN FOLDER, because butler pushes the FOLDER and not the publish. dotnet publish overwrites what
# it produces and deletes nothing else, so anything a previous build left behind was still being
# uploaded: this directory was shipping ResonanceHunter.Core.dll — the project's former assembly name,
# retired 2026-08-31 — months after the rename (found 2026-09-08 in the release sweep). butler's
# --if-changed diff means a clean rebuild costs no more bytes than a dirty one.
echo "== clean ${OUT}"
if [ -d "$OUT" ]; then find "$OUT" -mindepth 1 -delete; fi   # not `[ -d ] && …`: under set -e an absent dir would end the script

echo "== publish ${USERVERSION} → ${OUT}"
dn publish src/IdleXIdle.Game -c Release -r win-x64 --self-contained -o "$OUT" -v q --nologo \
  || { echo "publish failed" >&2; exit 1; }

# The evaluator's README travels with the build: what the game is, how to run it, where the save lives
# and how to reset it. One source (docs/publisher-notes.md), copied rather than duplicated.
cp docs/publisher-notes.md "$OUT/README.md"

echo "== smoke boot of the published exe"
SHOT="$(winpath "$PWD")\\build\\release\\smoke_${SHA}.png"
( cd "$OUT" && RH_SHOT="$SHOT" RH_SHOT_MODE=title ./IDLExIDLE.exe >/dev/null 2>&1 ) || true
[ -f "build/release/smoke_${SHA}.png" ] || { echo "the published build did not draw its title — not uploading" >&2; exit 1; }
rm -f "build/release/smoke_${SHA}.png"

echo "== butler push ${TARGET}:${CHANNEL} (userversion ${USERVERSION})"
"$BUTLER" push "$OUT" "${TARGET}:${CHANNEL}" --userversion "$USERVERSION" --if-changed
"$BUTLER" status "${TARGET}:${CHANNEL}" | tail -5
