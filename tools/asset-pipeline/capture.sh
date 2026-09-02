#!/bin/bash
# Capture a real screenshot of the game, headless.
#
#   bash tools/asset-pipeline/capture.sh [mode] [outfile]
#
# The game supports RH_SHOT=<path> (render 60 frames, save the canvas, exit) and
# RH_SHOT_MODE=<screen> to skip the title and open a screen directly. That makes
# the render loop verifiable from here instead of guessed at — every layout claim
# should be checked against this, not against the offscreen compositor.
#
# Modes: fight boss expedition forge build character stats warren map dust world
#        region2 region3 conquered lootforge reforge vow hybrid rig vfx help
#        buildtree buildzoom itemmenu runlog fightgear vaultfilter traitlit intro typespec welcome
#        vaultempty vaultemptyfilter vaultsell vaultmany forgeempty trainingpoor trainingreset
#        rosterswitch warrenready warrenfresh
#        fightshield fightshieldbroken fightstatus fightfive fightmulti fightreport
# The fight poses (UI polish §21 / §108): `fight` is the STANDARD (two actives, two passives, several
# creatures); `fightshield` a FULL wave-start shield; `fightshieldbroken` a bite heavy enough to empty
# it — the split bite and SHIELD BROKEN in one instant; `fightstatus` the UNDYING / CHARGE chips;
# `fightfive` the five-slot strip; `fightmulti` VOLLEY's CLUSTER folding five arrows into one "-N ×5".
# All of them take RH_SHOT_T=<seconds into the wave> (capture.sh's third argument). `fightreport` takes
# RH_SHOT_LIMIT=armour|reach|sustain to pose the log's diagnostic for that limit.
# `fightshieldbroken` and `fightmulti` aim the shutter at an EVENT (the first SHIELD BROKEN, SPRAY's
# cast) rather than a second: the seek applies two frames before the shot so the event is crossed live
# on the photographed frame (the replay is rebuilt from the wave's events, so it can land anywhere).
# RH_SHOT_LEAD=<seconds before the event> (0.02 default — under two frames keeps the event on the
# shot frame). RH_SHOT_DUMP=1 writes <shot>.events.txt — the wave's events and the playhead at the
# shutter — which is how a fight pose is checked against what the wave held.
# RH_SHOT_MOTION=<0..1> holds the CHROME's transients at that fraction of their run so they can be
#   photographed at all: 1 is the first instant of a screen switch, a modal fade, a currency pill's
#   spend flash and its banked "+N"; 0.5 is halfway; unset means the game animates normally.
# RH_SHOT_PAGE_MOUSE=x,y poses the cursor in PAGE space (a hover, a tooltip) on any menu screen.
# `forge` takes RH_SHOT_ITEM=<instanceId> (dev_hero | dev_rung | dev_cap | dev_low) and
# RH_SHOT_FILTER=all|gear|gems, so every item state is a dial rather than a new mode.
#
# `fight` takes a third argument: seconds into the wave to pose (default: about one).
# The fixture's creatures are thick on purpose, so a bar only visibly moves a few
# seconds in — pass 6 to photograph a damage number beside a bar it has drained.
# `vaultfilter` is the VAULT with the CHEST FILTER popover open and a setting in it (it moved off the HUNT).
# `welcome` is the HUNT under the WELCOME BACK panel after a simulated 6h 42m absence (real figures).
#
# `buildzoom` takes a third argument: the tree camera's zoom. With none it poses the
# FIRST-OPEN framing — the centre node and ring 1, which is what a player sees the
# first time — so `buildzoom` alone photographs the first visit. The node art is
# under thirty pixels across in the whole-tree overview, where a capture can only
# prove that something was drawn there — 0.40 frames a capstone medallion with its
# name, 0.95 the minors around the hub.
#
# `intro` takes a third argument too: which card of the first-run intro to pose,
# counted from 1 (default 1). The eight cards each spotlight a different region of
# the HUNT screen, and the only way to know the light frames the right thing is to
# look.
#
# `tour` poses any screen's first-visit tour — the same spotlight walkthrough as the
# intro, one per screen. Third argument: the screen (an Activity name — Stats, Gear,
# Build, Mastery, Vault, Forge, Warren, Map, Traits, Roster; Hunt is the intro).
# Fourth: the card, counted from 1 (default 1). The screen is dressed by the same
# fixture its plain capture uses, so the light falls on real content:
#
#   bash tools/asset-pipeline/capture.sh tour shot_tour_stats_1.png Stats 1
#
# `vaultfirst` poses a NEW GAME's vault — the one welcome gift SeedNewGame parks —
# which is the state the Vault's tour describes. `gemtour` poses the first-gem lesson:
# the forge fixture plus loose gems, with the lesson owed; its fourth argument is
# unused, so pass the card through RH_SHOT_STEP:
#
#   RH_SHOT_STEP=2 bash tools/asset-pipeline/capture.sh gemtour build/shots/gemtour_2.png
#
# `settingsopen` poses the SETTINGS panel with a dropdown list OPEN, so the list's own
# chrome -- its frame, its rows, its mark on the current value -- is photographable rather
# than described. RH_SHOT_DROPDOWN picks WHICH list: `mode` (the default) or `size`, the
# WINDOW SIZE list, whose entries depend on the desktop the capture runs on:
#
#   RH_SHOT_DROPDOWN=size bash tools/asset-pipeline/capture.sh settingsopen build/shots/sizes.png
#
# RH_SHOT_EXPLAIN=Stats,Map (any Activity names, SkillSlot2..4, or FirstGem) leaves those
# screens' tours (or slot notes) OWED under the rig, which otherwise treats everything
# as already explained so no tour sits over the thing a fixture is photographing.
# `tour` sets this for its own screen by itself.
#
# RH_SHOT_REDUCED=1 poses REDUCED MOTION — the accessibility setting that holds every idle
# animation still. It is set before the first Update, so the state is already on in the frame
# the shutter takes, and it is the only way to photograph it: the switch lives in SETTINGS and
# a capture never clicks. Unset means off, exactly like a fresh save. Works with every mode:
#
#   RH_SHOT_REDUCED=1 bash tools/asset-pipeline/capture.sh fight build/shots/fight_reduced.png
#
# `dust` takes the same third argument for the TRAIT tree's camera (its home zoom
# is about 0.55, fitting the whole tree; 1.2 frames one road with its art readable).
MODE="${1:-fight}"
OUT="${2:-shot_$MODE.png}"
ZOOM="${3:-}"
# `tour`'s third argument is a screen, not a dial — it must not reach RH_SHOT_ZOOM /
# RH_SHOT_T, which the screen's own fixture may read as a zoom or a pose time.
TOUR_TAB=""; TOUR_STEP=""
if [ "$MODE" = "tour" ]; then TOUR_TAB="${3:-Training}"; TOUR_STEP="${4:-1}"; ZOOM=""; fi
. "$(dirname "${BASH_SOURCE[0]}")/../shellenv.sh" || exit 1
cd "$(dirname "${BASH_SOURCE[0]}")/../.." || exit 1
WINDIR="$(winpath "$PWD")"
# BUILD FIRST. Asset PNGs are copied into the output directory as a build step, so a --no-build run
# renders whatever art was there last time. That silently verified a stale portrait once; a three
# second build is much cheaper than trusting a screenshot that lies.
dn build src/IdleXIdle.Game -v q --nologo >/dev/null 2>&1 \
  || { echo "build failed" >&2; exit 1; }
# RH_SHOT is read by a Windows process, so it must be a Windows path even when this shell
# is using /c or /mnt/c. OUT stays repo-relative — an absolute one fails with a bare
# "capture failed", because the check at the bottom looks for it relative to the repo.
RH_ENV=(RH_SHOT="$WINDIR\\$OUT" RH_SHOT_MODE="$MODE")
# `settingsopen`: which dropdown list to pose. Exported by the caller, forwarded here.
[ -n "$RH_SHOT_DROPDOWN" ] && RH_ENV+=(RH_SHOT_DROPDOWN="$RH_SHOT_DROPDOWN")
# UI SCALE for the page screens: 100 (the default) | 125 | 150 | auto.
[ -n "$RH_SHOT_UISCALE" ] && RH_ENV+=(RH_SHOT_UISCALE="$RH_SHOT_UISCALE")
# A REAL window size (e.g. 1280x720): the capture is then the presented backbuffer, not the 1920 render target.
[ -n "$RH_SHOT_WINDOW" ] && RH_ENV+=(RH_SHOT_WINDOW="$RH_SHOT_WINDOW")
[ -n "$RH_SHOT_MOTION" ] && RH_ENV+=(RH_SHOT_MOTION="$RH_SHOT_MOTION")
# REDUCED MOTION on, before the first Update — the accessibility state, posed rather than clicked.
[ -n "$RH_SHOT_REDUCED" ] && RH_ENV+=(RH_SHOT_REDUCED="$RH_SHOT_REDUCED")
# MASTERY: pin a node in the inspector (a MasteryCatalog id) for the buildtree/buildzoom modes;
# TRAITS: select a trait (a MemoryDust id) for the dust mode.
[ -n "$RH_SHOT_NODE" ] && RH_ENV+=(RH_SHOT_NODE="$RH_SHOT_NODE")
# The third argument means "zoom" to buildzoom and "seconds into the flourish" to
# traitlit/traitterm — both are the one dial that mode's capture needs.
[ -n "$ZOOM" ] && RH_ENV+=(RH_SHOT_ZOOM="$ZOOM" RH_SHOT_T="$ZOOM")
# `tour`: the screen and the card. The game resolves the mode to that screen's own fixture.
[ -n "$TOUR_TAB" ] && RH_ENV+=(RH_SHOT_TAB="$TOUR_TAB" RH_SHOT_STEP="$TOUR_STEP")
dn run --project src/IdleXIdle.Game --no-build >/dev/null 2>&1
[ -f "$OUT" ] && echo "captured $OUT ($(stat -c%s "$OUT") bytes)" || { echo "capture failed" >&2; exit 1; }
