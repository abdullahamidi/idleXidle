#!/bin/bash
# Does the game actually START — with a real save loaded?
#
#   bash tools/check_boot.sh
#
# NOT part of check_all.sh, which is the build-free set. This one builds and runs the game, so it
# costs ~40 seconds; run it before shipping anything that touches startup, saves or persistence.
#
# FOR A LONGER HUNT, drive the mode directly with a frame count:
#
#   RH_BOOTCHECK=18000 RH_SAVE_DIR="$(cygpath -w "$TEMP/rh_soak")" \
#     dotnet run --project src/IdleXIdle.Game --no-build
#
# That soaks the fight for about five minutes against a throwaway save directory, which is how the
# DrawComposition crash was found — a Math.Clamp whose minimum overtook its maximum once a wave grew
# wide enough. Nothing shorter reached a composition big enough to trigger it.
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

SAVE="${LOCALAPPDATA:-$HOME/.local/share}/IDLExIDLE/save.json"   # SaveFile.AppDataFolderName

dn build src/IdleXIdle.Game -v q --nologo >/dev/null 2>&1 \
  || { echo "build failed" >&2; exit 1; }

before=""
[ -f "$SAVE" ] && before="$(sha256sum "$SAVE" | cut -d' ' -f1)"
[ -n "$before" ] || echo "note: no save file at $SAVE — this run proves the FRESH path only."

# A WALL CLOCK on top of the in-game frame ceiling. Two independent stops, because they fail
# differently: the frame ceiling catches a walk that stops advancing, and this catches a process that
# never reaches Update at all. A hung run once held the built exe open and made the NEXT build fail
# with a file lock, which reads as a broken repo rather than a stuck test.
# Wrapped in a subshell because `dn` is a shell FUNCTION and timeout(1) can only wrap an executable —
# `timeout 120 dn ...` fails with "No such file or directory" and reports it as a boot failure, which
# is a check lying about the thing it is checking.
# Runs the game once under RH_BOOTCHECK. $1 is an optional RH_SAVE_DIR; empty means the real save.
boot_run() {
  timeout 120 bash -c '
    . tools/shellenv.sh || exit 1
    RH_ENV=(RH_BOOTCHECK=1 RH_LESSON_LEDGER=1)
    [ -n "$1" ] && RH_ENV+=(RH_SAVE_DIR="$1")
    dn run --project src/IdleXIdle.Game --no-build
  ' _ "$1" 2>&1
}

out="$(boot_run "")"
rc=$?

if [ $rc -eq 124 ]; then
  echo "BOOT TIMED OUT after 120s — the game never finished starting." >&2
  # Do not leave it holding the exe; the next build would fail on a file lock instead.
  powershell.exe -NoProfile -Command \
    "Get-Process IDLExIDLE -ErrorAction SilentlyContinue | Stop-Process -Force" >/dev/null 2>&1
  exit 1
fi

echo "$out" | grep -E "BOOT OK|Unhandled|Exception" | head -5

if [ $rc -ne 0 ] || ! echo "$out" | grep -q "BOOT OK"; then
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

# ── AND THE FIRST LAUNCH, which is a different branch and the one that hid the crash. ──────────
#
# With no file, LoadOrStartFresh returns early and seeds a new game — so the code that restores a
# save never runs, and any fault in it waits for the SECOND launch. That is precisely how the
# startup crash reached a player: launch one proved the game worked, launch two would not open.
# Testing it used to mean moving the real save aside, a procedure that only has to go wrong once.
# RH_SAVE_DIR points the whole save system at an empty directory instead.
FRESH="${TEMP:-/tmp}/rh_bootcheck_fresh"
mkdir -p "$FRESH"
rm -f "$FRESH/save.json"

fresh_out="$(boot_run "$(winpath "$FRESH")")"
fresh_rc=$?

echo "$fresh_out" | grep -E "BOOT OK|Unhandled|Exception" | head -3

if [ $fresh_rc -ne 0 ] || ! echo "$fresh_out" | grep -q "BOOT OK"; then
  echo "FIRST LAUNCH FAILED — a new player cannot start the game." >&2
  echo "$fresh_out" | tail -20 >&2
  exit 1
fi

# And the real save must still be untouched after a run that was pointed somewhere else entirely —
# the redirect is only useful if it actually redirects.
if [ -n "$before" ] && [ "$before" != "$(sha256sum "$SAVE" | cut -d' ' -f1)" ]; then
  echo "THE FRESH RUN TOUCHED THE REAL SAVE — RH_SAVE_DIR is not being honoured." >&2
  exit 1
fi

# ── THE ROUND TRIP, which is the half that costs an hour when it breaks. ───────────────────────
#
# Everything above proves the game READS. Writing is the failure with no symptom: you play, you
# close it, and the progress was never there. The fresh run above played for ten seconds inside a
# redirected directory and should have autosaved, so there is now a file to check — and a second
# run against the same directory has to come back reporting a save rather than a fresh start.
if [ ! -f "$FRESH/save.json" ]; then
  echo "NOTHING WAS SAVED — ten seconds of play in a clean directory produced no save.json." >&2
  echo "The game reads fine and would lose a session." >&2
  exit 1
fi

reload="$(boot_run "$(winpath "$FRESH")")"
echo "$reload" | grep -E "BOOT OK|Unhandled|Exception" | head -3

if ! echo "$reload" | grep -q "save loaded"; then
  echo "WHAT IT WROTE, IT CANNOT READ — the second run did not see a save." >&2
  echo "$reload" | tail -20 >&2
  exit 1
fi

if [ -n "$before" ] && [ "$before" != "$(sha256sum "$SAVE" | cut -d' ' -f1)" ]; then
  echo "THE ROUND-TRIP RUN TOUCHED THE REAL SAVE." >&2
  exit 1
fi

# ── AND THE ONE PIECE OF WIRING NO UNIT TEST CAN REACH. ───────────────────────────────────────
#
# Two migrations decide whether a loaded save is a VETERAN — one for the fall loop
# (OnboardingLessons.SeedFallLoopAsLived) and one for the explained list (Onboarding.SeedExplained)
# — and both read the same host field, Game1._saveVersionSeen, which is assigned from the loaded
# file's Version. The PREDICATES are unit-tested at both branches. The WIRING is not, and cannot be:
# Game1 needs MonoGame to instantiate, so there is no seam a unit test can hold. Extracting one
# would mean a fake host or an interface hierarchy for a single assignment, which is a worse trade
# than this.
#
# So it is proved where it actually runs. Two boots against a hand-written file that differs by one
# character, reading the ledger the game already prints (RH_LESSON_LEDGER).
#
# THE NEGATIVE CONTROL IS THE HALF THAT MATTERS: a `SaveGame.CurrentVersion` creeping into either
# call site would still pass the v4 lane and fail the v5 one.
SEED="${TEMP:-/tmp}/rh_bootcheck_seed"
mkdir -p "$SEED"

# MasteryEarned is what Game1 restores into _deepestEver, which LessonFactsNow hands to DeepestWave;
# 25 clears Checkpoints.ConquestWave (20), so the progression half of the seed's test is satisfied
# and only the VERSION decides. Every other member takes its default through System.Text.Json.
#
# THE TWO NUMBERS ARE FROZEN CONSTANTS, not "one less than current": 4 is a file from before the
# tours became opt-in, 5 is Onboarding.FirstVersionWithExplainedList and
# OnboardingLessons.FirstVersionWithFallLoopFacts. When CurrentVersion next moves, these do not —
# and if somebody makes them move, this lane is where it shows.
seed_version() {
  rm -f "$SEED/save.json" "$SEED/save.bak" "$SEED"/save.pre-v*.json
  printf '{ "Version": %s, "SavedAtMs": 1700000000000, "MasteryEarned": 25 }
' "$1" > "$SEED/save.json"
}

ledger_line() { echo "$1" | grep -m1 "ftue_loop_lived="; }
inbox_line()  { echo "$1" | grep -m1 "inbox.unread="; }

# THE INBOX RIDES THE SAME TWO LANES. Both files are below Dispatches.FirstVersionWithInbox (8), so
# both must be SEEDED — told what they already know, with nothing unread — and the seed must have
# marked something: MasteryEarned 25 opens TRAINING, and the starter is on every roster.
assert_inbox_seeded() {   # $1 = lane name, $2 = the run's output
  local line
  line="$(inbox_line "$2")"
  if [ -z "$line" ]; then
    echo "NO INBOX LEDGER ROW on the $1 lane — DumpLessonLedger printed no inbox line, so this proves nothing." >&2
    exit 1
  fi
  if ! echo "$line" | grep -q "seeded=True"; then
    echo "A PRE-v8 SAVE WAS NOT SEEDED — Game1._saveVersionSeen is not reaching Dispatches.SeedKnown." >&2
    echo "  ledger: $line" >&2
    exit 1
  fi
  if ! echo "$line" | grep -qE "unread=0[[:space:]]"; then
    echo "A PRE-v8 SAVE BOOTED WITH UNREAD MAIL — a veteran was handed letters about a life already led." >&2
    echo "  ledger: $line" >&2
    exit 1
  fi
  if echo "$line" | grep -qE "known=0[[:space:]]"; then
    echo "THE SEED MARKED NOTHING KNOWN — the file's facts are not reaching it." >&2
    echo "  ledger: $line" >&2
    exit 1
  fi
  echo "$1 save's inbox seeded as already known — $line"
}

seed_version 4
old_out="$(boot_run "$(winpath "$SEED")")"
old_line="$(ledger_line "$old_out")"
if [ -z "$old_line" ]; then
  echo "NO LESSON LEDGER — RH_LESSON_LEDGER printed nothing, so this lane proves nothing." >&2
  echo "$old_out" | tail -20 >&2
  exit 1
fi
if ! echo "$old_line" | grep -q "ftue_loop_lived=True"; then
  echo "A PRE-v5 SAVE WAS NOT SEEDED AS A VETERAN — Game1._saveVersionSeen is not reaching the seed." >&2
  echo "  ledger: $old_line" >&2
  exit 1
fi
echo "v4 save seeded as a veteran."
assert_inbox_seeded v4 "$old_out"
# ...and the bump that made the seed safe took its snapshot: a pre-v8 file is copied aside before the
# first autosave can rewrite it (SaveStore.SnapshotBeforeUpgrade, once per target version).
if ! ls "$SEED"/save.pre-v8-*.json >/dev/null 2>&1; then
  echo "NO PRE-UPGRADE SNAPSHOT — a pre-v8 file booted without save.pre-v8-*.json being taken." >&2
  exit 1
fi
echo "pre-v8 snapshot taken."

seed_version 5
new_out="$(boot_run "$(winpath "$SEED")")"
new_line="$(ledger_line "$new_out")"
if [ -z "$new_line" ]; then
  echo "NO LESSON LEDGER on the current-version lane." >&2
  exit 1
fi
if ! echo "$new_line" | grep -q "ftue_loop_lived=False"; then
  echo "A CURRENT-VERSION SAVE WAS SEEDED AS A VETERAN — the version has stopped deciding, and a new" >&2
  echo "player who reloads loses READ THE LOG, MAKE ONE CHANGE, and the retry that completes the loop." >&2
  echo "  ledger: $new_line" >&2
  exit 1
fi
echo "v5 save believed as written — false facts stay false."
assert_inbox_seeded v5 "$new_out"

# ── AND THE NEGATIVE CONTROL FOR THE INBOX: a file AT the inbox version is believed as written. ──
#
# A hand-written v8 file carrying ONE unread letter must boot with that letter still unread and
# nothing seeded. A `SaveGame.CurrentVersion` creeping into the seed's gate — or the gate reading the
# inbox's emptiness instead of the file's version — would pass both lanes above and fail here.
seed_v8_with_one_unread() {
  rm -f "$SEED/save.json" "$SEED/save.bak" "$SEED"/save.pre-v*.json
  printf '{ "Version": 8, "SavedAtMs": 1700000000000, "MasteryEarned": 25, "Dispatches": [ { "Key": "region.verdant_hollow.conquered", "Kind": "Region", "AtMs": 1700000000000, "Read": false, "RegionId": "verdant_hollow" } ], "KnownDispatchKeys": [ "region.verdant_hollow.conquered" ] }
' > "$SEED/save.json"
}

seed_v8_with_one_unread
v8_out="$(boot_run "$(winpath "$SEED")")"
v8_line="$(inbox_line "$v8_out")"
if [ -z "$v8_line" ]; then
  echo "NO INBOX LEDGER ROW on the v8 lane." >&2
  echo "$v8_out" | tail -20 >&2
  exit 1
fi
if ! echo "$v8_line" | grep -q "seeded=False"; then
  echo "A CURRENT-VERSION SAVE WAS SEEDED — the version has stopped deciding, and every letter a new" >&2
  echo "player has not opened yet would be marked as already known on their next launch." >&2
  echo "  ledger: $v8_line" >&2
  exit 1
fi
if ! echo "$v8_line" | grep -qE "unread=1[[:space:]]"; then
  echo "AN UNREAD LETTER DID NOT SURVIVE THE BOOT — a v8 file is not being believed as written." >&2
  echo "  ledger: $v8_line" >&2
  exit 1
fi
echo "v8 save believed as written — an unread letter stays unread, nothing seeded — $v8_line"

if [ -n "$before" ] && [ "$before" != "$(sha256sum "$SAVE" | cut -d' ' -f1)" ]; then
  echo "THE MIGRATION LANES TOUCHED THE PLAYER'S SAVE." >&2
  exit 1
fi

# ── AND THE AUTHORED OPENING, LIVE. ────────────────────────────────────────────────────────────
#
# Every other harness path either skips the opening (the shutter, so it is not in 400 captures) or
# freezes one beat of it for a photograph. Neither runs the MACHINE — the per-frame update, the
# fight hold, the replay barrier, the screen grants and the surface — which is the part a player
# actually meets and was the only part of this with no headless check at all. This lane starts a
# brand-new career INSIDE the opening and soaks it for the full boot walk: the fight is held from
# the arrival on, so a hold that deadlocked the frame or a card that threw would take the process
# down here rather than on someone's first launch.
OPENING="${TEMP:-/tmp}/rh_bootcheck_opening"
mkdir -p "$OPENING"
rm -f "$OPENING/save.json"

opening_out="$(timeout 120 bash -c '
  . tools/shellenv.sh || exit 1
  RH_ENV=(RH_BOOTCHECK=1 RH_SHOT_OPENING=live RH_SAVE_DIR="$1")
  dn run --project src/IdleXIdle.Game --no-build
' _ "$(winpath "$OPENING")" 2>&1)"
opening_rc=$?

echo "$opening_out" | grep -E "BOOT OK|Unhandled|Exception" | head -3

if [ $opening_rc -ne 0 ] || ! echo "$opening_out" | grep -q "BOOT OK"; then
  echo "THE AUTHORED OPENING CANNOT BE ENTERED — a brand-new career cannot start the game." >&2
  echo "$opening_out" | tail -20 >&2
  exit 1
fi
echo "the authored opening runs live, and the frame survives it."

if [ -n "$before" ] && [ "$before" != "$(sha256sum "$SAVE" | cut -d' ' -f1)" ]; then
  echo "THE OPENING LANE TOUCHED THE PLAYER'S SAVE." >&2
  exit 1
fi

echo "boot green — reads, writes, reads back what it wrote, and migrates by version."
