# BRAND, the MARK / PERSISTENT TARGET-ATTACHED STATE reference: discovery (ADR-011, 2026-09-29)

Written BEFORE any change. The fight's rules were read in `src/IdleXIdle.Core` and then re-derived by RUNNING the real
Core in a throwaway probe (three builds per variant, seeded, every Marked event and every hit ratio printed); the screen
was read in `src/IdleXIdle.Game` and filmed (`build/shots/brand/b00`, the seeded Seeker fight, BRAND woven into PRESS's
slot). Five read-only audit lenses + one probe verifier.

## 1. What BRAND really does (the gameplay truth, probe-verified)

| Question | Answer (file:line) |
|---|---|
| Kind | a FIELD (passive, no beat, no cast, no champion clip): `sign_brand`, SkillKind.Field, SkillEffect.Amplify, IntervalMs 2000 (SkillCatalogue.cs:553) |
| Who is marked | the FRONT enemy: the first creature still standing in spawn order (`FirstAlive`, SoloBattle.cs:1414, 1547). Base BRAND: front x1.70, the rest x1.00. SPRAWL: every creature x1.35 (EVEN x1.525). ANCHOR: front x1.70, the rest at the spread depth. Every hit through `LandSpread` is amplified, the swing too |
| When it starts | the wave's FIRST field tick, always 2000 ms into the wave (1000 with PACE); no mark before it. A wave shorter than 2 s never has one |
| How long it persists | refreshed every tick for one interval (x1.3 / x1.8 with the mastery and Linger); in practice CONTINUOUS from the first tick to the wave's end. It ends only when the wave ends (cleared, wiped, stalled); nothing carries between waves |
| Deepen / stack | base BRAND never deepens (70 %). ETCH deepens on EVERY tick, the first included: 120, 170, 220, 240 (cap). SINK 150, 230, 240. GRAVEN 120 ... 320. WINNOW (SPRAWL) 45 ... 85. The depth belongs to the WAVE (`markDeepen`, SoloBattle.cs:1125), never reset by a death |
| Spread / transfer | there is no transfer event and no transfer rule: "front" is re-evaluated on every hit, so when the front dies the NEXT creature is the front and is amplified at once (even between two arrows of one SPRAY). Its depth is unchanged (ETCH's "keeps its depth when it moves" is simply true). The only signal is the old host's `EnemyDown` |
| Consume / detonate | NONE. BRAND is never spent, never detonates ("MARK detonation" survives only in two stale comments) |
| What the screen is told | per tick: `Aura(slot)` then `Marked(Slot = depth %, Amount = window ms)`; no target. The front is derivable from the replay (lowest `CreatureAlive`, matches FirstAlive in every sampled frame) |

**One reporting bug in Core.** On the field path `Marked` is emitted BEFORE the depth is updated
(SoloBattle.cs:2434 vs 2450), so it reports the PREVIOUS tick's depth: the first tick of every wave says 0 %, ETCH is
always one step late, and the inspector reads "+0 %" for two seconds of every wave. The cast path and OATHMARK report
after the update. The presentation needs the depth in force.

## 2. What is on screen today (measured)

| | Measured |
|---|---|
| On the marked enemy | NOTHING. No spawn, no badge, no flash, no number, no sound on any tick or on a host change (the take has 5 ticks and 2 host deaths; 0 mark spawns) |
| What BRAND draws | the generic held field: `fx_seeker_mark_strip8_512` held BEHIND THE CHAMPION (`FieldAura`, Standing, 1.10 x his height): a 518 px frame, 453 px of content, ~386 px ring around his body |
| The art | a clean white vector RETICLE: one ring, four cardinal ticks cycling Y / X / +, a double diamond with a bright pip. Pure greyscale, 24 % coverage, ~15k full-white pixels a frame (luma p95 255) |
| Its rhythm | a brightness spike every 1 s of wall clock (0.38 -> 1.0), unrelated to BRAND's 2 s tick |
| Where it reads | as the champion's aura / a target reticle around HIM, i.e. a cast or a UI lock-on, not a state on the enemy |
| Inspector | "MARKED YOUR SKILLS +{%}" on ANY hovered creature (even unmarked ones), +0 % for the first 2 s, and it says "skills" though the swing is amplified too |
| Also found | only the FIRST Field in a build is presented (HuntScreen.cs:1506): BRAND in an earlier slot than PRESS would silently drop PRESS's accepted picture and draw the reticle instead |

## 3. What is wrong

1. The mark is on the wrong body: it belongs to the enemy and is drawn around the hunter.
2. It is UI language: a crosshair reticle, white, pulsing on a timer, 518 px.
3. The state has no life: no apply, no host, no depth, no migration. The player cannot see who is marked, that it
   deepens (ETCH), or that it moved when the host died.
4. A Core report lags one tick, so any honest depth picture is impossible without it.
5. A second field in the build can erase an accepted picture (PRESS).

## 4. The sentence

> That enemy has been branded. The mark is living shadow, etched into its body. It quietly persists there. When the
> mark deepens, it bites further inward. If it moves, the same mark migrates cleanly to the next target.

Not a projectile, not a field, not a trap, not a champion performance: a STATE on the enemy.

## 5. Why BRAND is the right MARK reference

It is the purest persistent target-attached state in the catalogue: it deals nothing itself, it has no cast, its whole
meaning is "this creature takes more", it persists for the wave, it has a real depth ladder (ETCH), a real reach
variant (SPRAWL) and a real host change on every front death. It exercises every MARK moment except consume, which it
honestly does not have. CALL (a cast mark) and OATHMARK (a reaction mark) share its art key "mark" but are not states:
the recipe must be keyed by skill id and leave them untouched.

## 6. The presentation contract (the slice builds and is judged against this)

**Host.** Exactly the creatures Core amplifies: the front one (SPRAWL: every one standing; ANCHOR: the front at the
full form, the rest at the spread form). Never the champion: the reticle halo is retired for BRAND.

**Attachment.** ON the body, INSIDE the silhouette: at the torso's thickest interior (a distance-to-edge centroid of the
drawn frame's middle band), measured from the frame actually drawn, so it rides the idle breath, the lunge and PRESS's
buckle. Drawn in the creature loop right after the creature (a creature standing in front covers it) and BEFORE the hit
flash (a hit whitens the body and the brand together). Never above the head, never on the ground.

**Size.** ~0.34 of the canonical body height proposed; BUILT at 0.28 (`MarkRecipe.SizeShare`: a whelp's coil box ~60 px), constant: depth never changes the size.

**Material.** Etched Shadow Brand: an imperfect branded sigil (PixelLab-sourced silhouettes, deterministic assembly) in
the game's pixel material (logical pixel ~3 screen px, nearest, value bands, whole-pixel destinations), made of:
dark shadow INK inside the cut (near-black violet, it reads on light bodies), a lit ETCHED GROOVE (lavender-violet,
luma ~110-140 at rest: above the whelp's own violet rim ~55, far below white), soft smoky ROOTS bleeding out of the cut
(translucent), and a slow internal swirl. White appears nowhere; the brightest moment is one line.

**Moments.**
- APPLY (the wave's first tick): smoke gathers on the body and condenses into the sigil over ~4-6 steps, the groove
  cuts in as a lit line exactly ON the tick (the moment the mark becomes true), then settles. No burst, no ring.
- IDLE: the settled sigil with a very slow internal swirl; outline still; no pulse, no ring, no cloud around the body.
- REFRESH (a tick that changes nothing, base BRAND): nothing visible. A shown change must mean a changed number.
- DEEPEN (ETCH / WINNOW, the depth rose): the same sigil is cut deeper: grooves thicken, an inner layer appears, the
  ink fill grows denser, a secondary etched sub-shape appears; a one-line chisel beat on the tick. Never "just brighter",
  never bigger. The stage is a function of the depth in force (spread < 50 %, base < 100, then 100 / 150 / 200 / 240+).
- MIGRATE (the host dies): the mark loosens into smoke-ink threads on the dying body, the threads travel in a low arc to
  the new front and re-etch AT THE SAME STAGE (~300 ms). Migration, not a new cast. The last host dying: the mark
  loosens and is gone.
- SPREAD (SPRAWL): the mark forms on the front and threads spread it to every other creature, each at the spread form.
- CONSUME: none (BRAND has none).

**Hierarchy / overlaps.** Quieter than every accepted action: rest adds ~0 px above luma 210; a beat stays under PRESS's
tick (~750 px above luma 170). On a tick shared with PRESS's crush or a JAWS bite, the beat is quiet and the brand rides
the buckle; PRESS / JAWS draw over it. SPRAY / HARD HANDS flash the body and the brand together.

**Budget.** A handful of sprites per host, 0 bytes allocated per frame, no Effect, no per-entity batch switch.

**Core.** One information-only hook: emit the field tick's `Marked` AFTER the depth is updated, so it reports the
depth in force. Proof it changes no gameplay: the events list is write-only inside the fight; a test pins a
fingerprint of every other event and every wave outcome, taken before the change, byte-identical after it.

**As built (after five review rounds; see README.md).** Attachment: an AUTHORED body point per creature strip frame
(`<strip>.mark.json`, `mark_points.py`, reviewed on contact sheets, the halo kept off a reviewed head box) replaced the
torso centroid, which found the head on some families; the probe is a fallback only. Deepen: the thickened inner end read
as an S / 5 / 9 / @ and a lit knot as a pupil, so the hook stays a thin, never-lit line in a dark channel; the ring's
wall is cut into the hollow (deep2), then the hook is cut further along the spiral, inward (deep3, deep4: a burn spreading
out of the ring read as "just bigger" and a closed ring as an O / eye, so the outline never grows and the spiral stays
open); the chisel lights only the new cut; the smoke roots never fill the hollow (as ink on a light body they closed its
outlines into an eye) and never change between idle phases.
One SHOWN stage per coil carries a deepen across a fall. SPRAWL is death-aware and forms the row in ~0.6 s.

**Out of scope (reported, not changed).** ETCH's card says "to +170 %" while the code reaches +240 % (a design
decision); the inspector's "YOUR SKILLS" / any-creature wording; CALL + BRAND last-writer-wins and SPEND + BRAND
interplay; the tick-instant gap for a hit processed before BRAND's slot; the aura-blow misread when BRAND sits in a lower
slot than an Active casting on its tick; sound (a BRAND cue is a later audio pass, as PRESS's was).
