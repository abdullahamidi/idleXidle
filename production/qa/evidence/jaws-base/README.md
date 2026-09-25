# BASE JAWS — the REACTION / TRAP reference (ADR-011, 2026-09-25) — BUILT, AWAITING THE OWNER'S REVIEW

The owner approved the discovery (`../jaws-discovery/`):

- **D1:** the dock owns ARMED and REARMING; the world shows only the trigger (CONTACT → SNAP → TENSION → RECOIL →
  RELEASE).
- **D2:** IRON JAWS.
- **D3:** one information-only Core readiness event.

This folder is the base slice's evidence. NET and IRON art are not built, REPAY is untouched, and SPRAY and HARD HANDS
do not move (proved below).

Every film is the real game through the fight fixture, with JAWS woven in slot 3 (Shadow). They are SEEDED with
`RH_SHOT_SEED=7`, because the fight's crit dice are `new Random()` otherwise. Tracing is on (`RH_PRESENT_TRACE=1`), and
the recipe is `build/shots/jaws/films_jaws.sh`. Sound is rendered from the trace (`film_audio.py`: the cues the game
asked for, at their volumes and ducks). The snap is a CANDIDATE until heard.

## The visual sentence, measured (normal TEMPO, the bite at 7000)

Times are ms after the contact, on the fight's playhead.

| When | What |
|---|---|
| −883 | the pack starts its bite wind-up |
| −170 | the fight REPORTS JAWS armed (`ReactionArmed`: it rearmed in 2830 ms, not the card's 3000; the Seeker acts at ×1.06) |
| 0 (+17 frame) | the bite lands: the enemy's thud and the red burst on him (the cause) |
| same frame | JAWS: the open jaws start rising under the front creature's front-lower silhouette; `sfx_seeker_jaws_snap`; "−7 JAWS" and a soft flash on that creature |
| same frame | the chain whips out from the jaws toward his BELT |
| +38 → +50 | SHUT: the toothed ring closes on the limb with a 12 % overshoot; the teeth glint in the Source; a small flattened flash; five sparks |
| +50 → +95 | the chain goes taut, then a small decaying shiver |
| +50 → +250 | the recoil: the creature pushed a few pixels away, peaking early, eased back |
| +217 | release: the chain slackens, the jaws loosen, drop and fade |
| +333 | gone; nothing stays |
| +2830 | the dock's sweep completes at the fight's report, then waits 170 ms for the next bite |

The champion goes on doing what he was doing: frame 7 of a swing here. No clip, no cast, no lunge; the old post-bite
"lay a trap" clip is skipped for JAWS and not even loaded.

## The acceptance, point by point

| The owner's criterion | Evidence |
|---|---|
| the enemy bite clearly causes the retaliation | `01b`–`01d`, `02a`/`02b`: the jaws close on the creature on the bite's own frame, with THUD → CLACK |
| world art targets the actual reflected creature | the targets are the reflected `Strike` events; `15a` shows the clamp point (magenta) on the struck creature's own front-lower silhouette |
| the row-sized rope effect is gone | `01e` (OLD above, NEW below); JAWS never calls the legacy effect (`jaws_reaction_test`) |
| iron jaws are physically readable | `02b`, `20`: open U → toothed ring, lifted one shade so the iron reads on the floor |
| iron material, Source as accent light | untinted jaws and chain; the glint, flash, sparks and tension accent are the only Source |
| the chain connects the attacker to the MOVING Seeker | `06a`/`06b`: JAWS snaps at 13000 and HARD HANDS leaps at 13083; the chain's belt end rides the leap (64 → 16 links as he closes), the jaws stay on the brute, nothing is left at his start |
| the champion's action is never interrupted | `04a`/`04b` (reaction hidden): the swing and HARD HANDS untouched; `18`: every SPRAY / HARD HANDS moment identical with the layer on and off |
| redundant generic sound / VFX removed | the cast breath, the reaction thud, the answer's thud and puff, the row ring and the callout are gone for JAWS; the enemy's bite thud stays |
| the dock shows real Core-owned rearm state | `09a`/`09b`: the sweep runs to the fight's own report (normal TEMPO 2830 ms and a 170 ms wait; fast TEMPO 1583 ms and 417 ms), dims while rearming, and comes up with the gold ring alone |
| readable during SPRAY and HARD HANDS without overpowering them | `05a`/`05b`: the snap at 1000 in SPRAY's wind-up, the knives out at 1150, the jaws gone at 1333, before SPRAY's hit at 1400. Under an action the snap is ducked to 0.45 and the light plays at 0.55 |
| REPAY isolated | `14a`, `17`: REPAY keeps its "SNARE" callout, its row ring, its cast breath, its hit and its cast clip; only slot 3 (JAWS) ever spawns jaws or plays the snap |
| killing and champion-death edge cases coherent | `12a`/`12b`: JAWS' answer KILLS; the snap plays, the jaws let go at +83 as the creature falls, and the normal death follows. `13a`/`13b`: the bite fells the champion, JAWS still answers first, the jaws let go at +83, and his fall plays over it |
| repeated triggers are not exhausting | `08a` (16 s), `08b` (5 s at fast TEMPO), against the OLD `08c`: one short cue and a third of a second of picture per trigger |

## Files

| File | What |
|---|---|
| `01a` / `01b` | OLD (the discovery film) and NEW, true speed, sound effects only |
| `01c` / `01d` | NEW with the arena music; NEW muted |
| `01e` | OLD above, NEW below, paired by playhead, muted |
| `02a` / `02b` | the trigger 4× slower with its sound; every frame |
| `03a` / `03b` | EFFECTS ONLY (champion hidden) during HARD HANDS: the chain rides the hidden belt |
| `04a` / `04b` / `04c` | CHAMPION ONLY (the reaction layer hidden): a basic swing, HARD HANDS, every frame |
| `05a` / `05b` | JAWS during SPRAY |
| `06a` / `06b` | JAWS during HARD HANDS |
| `07a` / `07b` | JAWS on frame 5 of a basic swing |
| `08a` / `08b` / `08c` | repeated triggers: 16 s at normal TEMPO, 5 s at fast TEMPO, and the OLD 16 s |
| `09a` / `09b` | the dock tile through a whole rearm, labelled from the fight's own reports |
| `12a` / `12b` | the killing answer (`RH_SHOT_ENEMY=20,420`, the second run) |
| `13a` / `13b` | the champion falls on the triggering bite (`RH_SHOT_ENEMY=1400,700`, the second run) |
| `14a` | REPAY beside JAWS, 16 s |
| `15a` | the anchor overlay: the belt (cyan), the clamp point (magenta), the caught body's outline, riding the recoil |
| `16a` | the callout ON (above) against OFF (below, the slice) |
| `17_timelines.md` | every trigger's whole life (`reaction_timeline.py --report`) |
| `18_non_regression.md` | SPRAY and HARD HANDS identical with the reaction layer on and off (`action_regression.py`) |
| `20` | the art: open, shut, the glint mask, the chain link, and the two states at play size |

## Performance (1,408 traced reaction frames, every film)

- **Reactions alive:** at most one at a time. The list is bounded at 4.
- **Sprites:** at most 135: 64 chain links, their tension accent, the jaws, the glint, the flash and 5 sparks.
- **Draw calls:** at most 45 in the window from the reaction's material to its light. This is an upper bound, because
  the effects pass drawn between them is counted too. The light shares the frame's one additive batch.
- **Allocations:** 0 bytes on every frame (`alloc=` in `reaction-draw`). A trigger allocates its own small arrays
  once, and a creature's silhouette is probed once per (texture, frame) and cached.
- **Overlap with SPRAY and HARD HANDS:** no extra batches, and the actions' own figures are unchanged.

## Decisions recorded

- **The callout: off.** `16a` shows "SNARE" every two or three seconds over the number that already says "−7 JAWS".
  The jaws, the number and the dock say it. `RH_REACTION_CALLOUT=1` restores it.
- **Actions ignore the recoil.** A HARD HANDS leap planned into a recoiling creature measured the push (671 → 673 px).
  Actions now aim at the creature's own place, and only the reaction sees the recoil.
- **A zero-ms armed window cannot be filmed on this Seeker.** The fight checks readiness on 100 ms ticks, and his
  rearm (2830 ms, 1583 ms at fast TEMPO) is not a whole number of ticks. The discovery's "0 ms" came from adding
  RearmMs to the last trigger, the reconstruction D3 forbids. The 0 ms case is posed and its order (ARMED, then the
  trigger) proven in `reaction_armed_test`, and the dock's reading of it in the replay test.
- **PixelLab jobs.**
  - `2126658f`: pixflux open jaws, USED.
  - `b3c7ee25`: edit of it into the shut jaws, USED.
  - `891590a1`: pixflux, REJECTED (a spiked horn collar; its teeth vanished at play size).
  - About 22 generations spent in total.

## Still open

- **The snap is unheard.** See `design/audio/seeker-jaws-audio-brief.md`.
- **A pre-existing gate failure:** `tools/check_boot.sh` fails on this branch since 2026-09-21. It looks for a
  `save.pre-v8-*` snapshot, but the save version became 9 in `8314b03`. The same script with `pre-v9` passes in full.
  It is not touched here.
