#!/bin/bash
# Compile every custom effect to a committed OpenGL binary (ADR-013 §7).
#
#   bash tools/shaders/build_shaders.sh
#
# CI builds on Ubuntu, where mgfxc needs Wine, so no .fx goes through Content.mgcb. Instead each
# src/IdleXIdle.Game/Content/Shaders/<Name>.fx is compiled HERE, on Windows, with the MGCB tool's own
# mgfxc (/Profile:OpenGL, the DesktopGL backend) to assets/shaders/<snake_name>.mgfxo, which is committed,
# copied beside the game like the PNGs and loaded with new Effect(device, bytes).
#
# Beside each binary, <snake_name>.mgfxo.source-sha256 records the SHA-256 of the .fx it was built from
# (line endings normalised to LF, so a CRLF checkout hashes the same). brand_curse_shader_test fails when
# a shader was edited and not rebuilt.
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")/../.." || exit 1

MGFXC="${MGFXC:-$HOME/.nuget/packages/dotnet-mgcb/3.8.4/tools/net8.0/any/mgfxc.dll}"
[ -f "$MGFXC" ] || { echo "mgfxc not found at $MGFXC (run 'dotnet tool restore' in src/IdleXIdle.Game, or set MGFXC)" >&2; exit 1; }

SRC=src/IdleXIdle.Game/Content/Shaders
OUT=assets/shaders
mkdir -p "$OUT"

built=0
for fx in "$SRC"/*.fx; do
  [ -e "$fx" ] || continue
  name=$(basename "$fx" .fx)
  # PascalCase -> snake_case (BrandCurse -> brand_curse)
  snake=$(printf '%s' "$name" | sed -E 's/([a-z0-9])([A-Z])/\1_\2/g' | tr '[:upper:]' '[:lower:]')
  dest="$OUT/$snake.mgfxo"
  echo "mgfxc $fx -> $dest"
  dotnet "$MGFXC" "$fx" "$dest" /Profile:OpenGL
  tr -d '\r' < "$fx" | sha256sum | cut -d' ' -f1 > "$dest.source-sha256"
  built=$((built + 1))
done
echo "built $built shader(s)"
