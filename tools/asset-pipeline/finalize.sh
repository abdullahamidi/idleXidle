#!/bin/bash
# Wait for every generation job to finish, then leave the repo in a testable
# state. Runs unattended so the final build happens even if nobody is watching.
#
# The build matters: ResonanceHunter.Game.csproj copies assets/art/** into
# bin/Debug/net8.0/assets/art, and AssetLibrary loads from the BIN copy, not the
# repo. Regenerating art without rebuilding means testing yesterday's pictures.

cd "$(dirname "${BASH_SOURCE[0]}")/../.." || exit 1
LOG=tools/asset-pipeline/.staging
stamp() { date +%H:%M:%S; }

echo "[$(stamp)] waiting for generation jobs to drain..."
while pgrep -f "python3 -u tools/asset-pipeline/(animate|generate)\.py" >/dev/null; do
  sleep 60
done
echo "[$(stamp)] generation idle"

# Statics are cut from the strips, so this must run after the strips exist.
echo "[$(stamp)] deriving statics"
python3 -u tools/asset-pipeline/derive_statics.py 2>&1 | tail -20

# A regenerated key can land at a second path; collapse before shipping.
echo "[$(stamp)] collapsing duplicate keys"
python3 -u tools/asset-pipeline/dedupe_keys.py --apply 2>&1 | tail -3

echo "[$(stamp)] audit"
python3 -u tools/asset-pipeline/audit.py 2>&1 | head -25

echo "[$(stamp)] build"
"/mnt/c/Program Files/dotnet/dotnet.exe" build src/ResonanceHunter.Game/ResonanceHunter.Game.csproj -v q --nologo 2>&1 | tail -5

echo "[$(stamp)] runtime assets: $(find assets/art -name '*.png' | wc -l)"
echo "[$(stamp)] build output  : $(find src/ResonanceHunter.Game/bin/Debug/net8.0/assets/art -name '*.png' 2>/dev/null | wc -l)"
python3 - <<'PY'
import os, datetime
CUT = datetime.datetime(2026, 8, 10, 0, 0).timestamp()
new = old = 0
stale = []
for dp, _, fs in os.walk('assets/art'):
    for f in fs:
        if not f.endswith('.png'):
            continue
        if os.path.getmtime(os.path.join(dp, f)) >= CUT:
            new += 1
        else:
            old += 1
            stale.append(f[:-4])
print(f"converted to new art: {new}/{new+old}")
if stale:
    print("still legacy:", ", ".join(sorted(stale)))
PY
echo "[$(stamp)] FINALIZE DONE"
