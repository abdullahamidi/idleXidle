#!/bin/bash
# THE ACTOR GEOMETRY POSES (2026-09-06), at 100 / 125 / 150 — the fixture is this recipe. Every capture
# carries the geometry overlay (RH_SHOT_GEOMETRY=1: each figure's BODY bright, its ENVELOPE dim, the
# hunter's KEEP-OUT in ember) and writes <shot>.actors.txt beside it (RH_SHOT_DUMP=1: body / envelope /
# keep-out / box per subject, canvas px, the arena's own hover verdict, and CAPPED or GRANTED per
# actor) — numbers first, picture second.
#
#   GEOMETRY   vfxdebug     the seeker, QUIVER (the widest idle) and THE OATHBOUND (the narrowest):
#                           three silhouettes, the keep-out against the old 400x430 box
#   SWING      fightswing   the strike at its apex (0.55) — the blade inside the stable envelope
#   HOVER      fightinspect the pointer at the right-edge creature's envelope edge, PAST its idle body
#                           (a plate) and 20 px further out (no plate). The row drifts ±10 px between
#                           runs with the lunge clock, so the probes sit 10 px inside the edge; read
#                           the .actors.txt of the run before doubting a miss.
#   CAPPED     vfxdebug     THE ONE ACTOR THE RENDERER SHRINKS. A box may ask for more magnification
#              + fight      than UiKit.RasterCeiling allows, and only the 488x492 box asks it of an
#                           idle as deeply letterboxed as the Flayed Brute's (Marrow Wastes, 120 px of sky; the
#                           Dusk Ape's is the same): it asks 1.255 and is drawn at 1.25.
#
#                           THAT BOX IS EVERY BRUISER WAVE since 2026-09-07: a Bruiser rolls exactly one
#                           creature (Archetypes.Shapes, min 1 max 1) and a lone creature now wears its
#                           archetype's scale like a pack does. RH_SHOT_ARCHETYPE forces the scale and
#                           RH_SHOT_SOURCE the figure on any normal wave; only a BOSS wave refuses it
#                           (HuntScreen.RequireArchetypeReached throws), because a boss has its own
#                           presentation law and the dial would change nothing.
#
#                           A CAPPED pose is only honoured when its dump shows a creature `box` of
#                           488x492 and a `MAGNIFIED` verdict. `GRANTED` there means the ceiling was
#                           not reached; if the run did not throw and did not cap, the pose is broken.
#
#   FALLBACK   fight        RH_SHOT_NOSTRIP=1 takes every animation strip away, so the arena draws its
#                           STATIC POSES — the path no shipped asset can reach and nobody had seen.
#                           The geometry falls back WITH the draw: the dump reads `STILL`, and a still
#                           has one silhouette, so body and envelope are the same rectangle. The
#                           champion also reads `CROPPED`, because his base design is drawn at a fixed
#                           0.02 crop rather than his measured headroom — the draw's own long-standing
#                           choice, which the geometry now matches instead of contradicting.
#
#   SCALE      fight        ARCHETYPE SCALE IS POPULATION-INDEPENDENT (2026-09-07). A lone creature used
#              x6           to be laid out at the UNSCALED 436x440 enemy box whatever its archetype, so
#                           the one archetype that always rolls alone — the Bruiser — never wore its own
#                           size (360x421 alone against 403x471 in a pair, on the same art). Every normal
#                           wave goes through HuntScreen.CreatureRow now: the same archetype box for one
#                           creature as for each of a pack, and population decides only room, placement
#                           and spacing. RH_SHOT_CREATURES=<n> draws the first n of the wave's creatures
#                           — nothing is composed, the wave is the wave — so LONE and PACK of one
#                           archetype can be photographed at one camera: Bruiser, Swarm, and Armoured as
#                           the near-neutral reference. Read each pair's .actors.txt: the creature BODY
#                           sizes must agree lone-vs-pack (the pack's own x drifts ±10 px). NO SCALE IS
#                           TUNED by this fixture; the table is the design's.
#
# Usage: bash tools/asset-pipeline/hunt_geometry_fixtures.sh <tag>
# Writes build/shots/polish/<tag>_*.png (+ .actors.txt, .events.txt) — see polish_shots.sh.
set -u
cd "$(dirname "${BASH_SOURCE[0]}")/../.." || exit 1
TAG="${1:?tag}"
G="RH_SHOT_GEOMETRY=1;RH_SHOT_DUMP=1"
CAP="RH_SHOT_SOURCE=Body;RH_SHOT_ARCHETYPE=Bruiser;RH_SHOT_DUMP=1"
NS="RH_SHOT_NOSTRIP=1;RH_SHOT_DUMP=1"
bash tools/asset-pipeline/polish_shots.sh "$TAG" \
  "vfxdebug:100:RH_SHOT_DUMP=1" "vfxdebug:125:RH_SHOT_DUMP=1" "vfxdebug:150:RH_SHOT_DUMP=1" \
  "vfxdebug:100:RH_SHOT_HUNTER=quiver;RH_SHOT_DUMP=1" "vfxdebug:150:RH_SHOT_HUNTER=quiver;RH_SHOT_DUMP=1" \
  "vfxdebug:100:RH_SHOT_HUNTER=oathbound;RH_SHOT_DUMP=1" "vfxdebug:150:RH_SHOT_HUNTER=oathbound;RH_SHOT_DUMP=1" \
  "fightswing:100:$G;RH_SHOT_SWING=0.55" "fightswing:150:$G;RH_SHOT_SWING=0.55" \
  "fightinspect:100:RH_SHOT_T=7;$G" "fightinspect:125:RH_SHOT_T=7;$G" "fightinspect:150:RH_SHOT_T=7;$G" \
  "fightinspect:100:RH_SHOT_T=7;$G;RH_SHOT_CANVAS_MOUSE=1730,760" \
  "fightinspect:100:RH_SHOT_T=7;$G;RH_SHOT_CANVAS_MOUSE=1750,760" \
  "fightinspect:125:RH_SHOT_T=7;$G;RH_SHOT_CANVAS_MOUSE=1730,700" \
  "fightinspect:125:RH_SHOT_T=7;$G;RH_SHOT_CANVAS_MOUSE=1750,700" \
  "fightinspect:150:RH_SHOT_T=7;$G;RH_SHOT_CANVAS_MOUSE=1730,700" \
  "fightinspect:150:RH_SHOT_T=7;$G;RH_SHOT_CANVAS_MOUSE=1750,700" \
  "vfxdebug:100:$CAP" "vfxdebug:125:$CAP" "vfxdebug:150:$CAP" \
  "fight:100:$CAP;RH_SHOT_T=1.2" "fight:125:$CAP;RH_SHOT_T=1.2" "fight:150:$CAP;RH_SHOT_T=1.2" \
  "fight:100:$NS;RH_SHOT_T=1.2" "fight:125:$NS;RH_SHOT_T=1.2" "fight:150:$NS;RH_SHOT_T=1.2" \
  "fight:100:RH_SHOT_CREATURES=1;RH_SHOT_ARCHETYPE=Bruiser;RH_SHOT_DUMP=1;RH_SHOT_T=1.2" \
  "fight:100:RH_SHOT_CREATURES=2;RH_SHOT_ARCHETYPE=Bruiser;RH_SHOT_DUMP=1;RH_SHOT_T=1.2" \
  "fight:100:RH_SHOT_CREATURES=1;RH_SHOT_ARCHETYPE=Swarm;RH_SHOT_DUMP=1;RH_SHOT_T=1.2" \
  "fight:100:RH_SHOT_ARCHETYPE=Swarm;RH_SHOT_DUMP=1;RH_SHOT_T=1.2" \
  "fight:100:RH_SHOT_CREATURES=1;RH_SHOT_ARCHETYPE=Armoured;RH_SHOT_DUMP=1;RH_SHOT_T=1.2" \
  "fight:100:RH_SHOT_CREATURES=2;RH_SHOT_ARCHETYPE=Armoured;RH_SHOT_DUMP=1;RH_SHOT_T=1.2"
