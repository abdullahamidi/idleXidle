# ADR-011 — An action is performed, and its contact is the beat

| Field | Value |
|---|---|
| **Status** | **Accepted** (2026-09-25). The owner approved the Seeker's SPRAY as the project's FIRST GOLD-STANDARD AUDIOVISUAL COMBAT ACTION: its pictures, its timing and its three sounds (human-listened and approved). SPRAY is the reference implementation of this decision (below). Evidence: `production/qa/evidence/action-presentation-slice/` (`polish/`, `handoff/`). The Seeker's HARD HANDS is ACCEPTED (2026-09-25) as the MELEE / DIRECT-CONTACT reference: its pictures, its timing and its two sounds (human-listened and approved). Evidence: `production/qa/evidence/hard-hands/` (`pass2/`). SPRAY is the PROJECTILE / TRAVEL reference. The Seeker's JAWS, the proposed REACTION / TRAP reference: its architecture is APPROVED (skill-only recipe, the reaction layer, the Core `ReactionArmed` truth, the Core-driven dock, real targets, isolation, deaths); its motion and timing were rebuilt in the polish pass (the owner: "now good"); its SEMANTIC IDENTITY was rebuilt in the identity pass (a clamp in place of the crocodile head, a dry steel clack in place of a bone crunch: the sound direction is kept as the baseline) and its WORLD PROP and SNAP TIMING in the readable-clamp pass (2026-09-25): a spring-loaded BEAR TRAP whose two broad jaws dominate, closing over ~50 ms the eye can see, the answer landing on the stop (`production/qa/evidence/jaws-readable/`; before: `jaws-identity/`, `jaws-polish/`, `jaws-base/`). The owner did NOT approve the bear trap (2026-09-25) and, after a concept study and three enemy-bite foundation passes (ADR-012), set the PRODUCTION DIRECTION on 2026-09-27 (SHADOW PIRANHA retaliation, a stylised reactive-damage phrase; no bear trap, chain, housing, tether, yank or reel-in, no bite glyphs on the champion, no mirrored incoming/outgoing symbols, no Concept D) and, after three piranha passes (three jaws, a hero chomp, one piranha in negative space) still read as coloured motion at true speed, set the FINAL direction the same day: JAWS is **SHADOW FANGS**, a simple reactive-damage effect (enemy hits the Seeker → four LARGE SIMPLE Shadow fang shapes appear OPEN round the attacker → SNAP onto its outer silhouette → HOLD → "−X JAWS" → release, gone in ~200 ms). No creature to identify: the shapes are the effect. It is built (`ReactionRecipe` / `ReactionPerformance`, ONE hand-authored fang drawn four times, `sfx_seeker_jaws_fangs`, no JAWS target flash, the number delayed past the snap) and filmed (`production/qa/evidence/jaws-fangs/`); the piranha and the mechanical assets stay on disk as history and are never played. The acceptance question is one: at true speed, does it read as "something sharp / jaws snapped onto that enemy because it hit me"? On 2026-09-28 the owner answered: the teeth do not close, and the teeth themselves should be mist that comes, bites and vanishes; then, on the misty teeth: not four triangles, a jaw that really opens and closes (a piranha jaw); then, on the side-view maw that followed: "a bad jaw biting from the side; I meant the earlier perspective, biting from the FRONT, but a more effective and beautiful jaw", with Roni Kangaskorte's "Bite VFX" as the reference. JAWS is **A FRONTAL SHADOW BITE** (a crown of fangs and a lower row of SMOKE condense around the attacker's head, charge, slam shut solid and white-hot for one frame, and burst; ~677 ms), its cue a REAL bite built from CC0 foley and shaped to the owner's reference (Trundle's Q). **On 2026-09-28 the owner ACCEPTED JAWS as the GOLD-STANDARD REACTION reference** (A is perfect, exactly what I wanted. We can close JAWS with this sound effect and mark it as a gold standard.): its picture, its timing and its sound (human-listened and approved: `sfx_seeker_jaws_bite`, candidate A). Evidence: `production/qa/evidence/jaws-smoke-teeth/`, `jaws-real-bite/`. JAWS is CLOSED. The Seeker's PRESS, the PERSISTENT FIELD / AURA candidate (first slice, pixel-hard pass and field-vs-projectile polish, 2026-09-28; its picture APPROVED at commit `fab25919`), got its final audio on 2026-09-29: ONE composite tick cue (a pressure release, a compression thump, a brittle defence crack) from CC0 foley. **On 2026-09-29 the owner ACCEPTED PRESS as the GOLD-STANDARD FIELD / AURA reference** (PRESS is APPROVED. Use Candidate A - BALANCED as the final human-approved PRESS tick cue.): its picture, its timing and its sound (human-listened and approved: `sfx_seeker_press_tick`, candidate A). Evidence: `production/qa/evidence/press-propagation/`, `press-audio/`. PRESS is CLOSED. The Seeker's BRAND, the MARK / PERSISTENT TARGET-ATTACHED STATE candidate (the Etched Shadow Brand), is a FIRST SLICE awaiting the owner (2026-09-29, six review rounds), and its SECOND PASS, the Etched Shadow Cut (2026-09-30: the same architecture, a dark incision with a one-sided rim, three visible depths), awaits the owner too: see "The mark reference: BRAND" below; evidence `production/qa/evidence/brand-mark/` and `brand-cut/`. **BRAND is ACCEPTED (2026-10-02) as the GOLD-STANDARD MARK / PERSISTENT TARGET AFFLICTION reference** (visual, renderer ADR-013 and audio Candidate A, all owner-approved). BRAND is CLOSED. |
| **Date** | 2026-09-24 (proposed) · 2026-09-25 (accepted) · 2026-09-28 (JAWS accepted, the reaction reference; PRESS, the field reference, first slice) · 2026-09-29 (PRESS accepted, the field / aura reference; BRAND, the mark reference, first slice) |
| **Deciders** | user (approved the discovery; decided one knife per struck enemy, a physical knife scale, CONTACT on the beat, and a thrown-blade travel) + lead-programmer, technical-artist |
| **Related** | ADR-009 (the light every emissive layer draws through), ADR-010 (the projectile composite this reuses), `production/qa/evidence/action-presentation-audit/` (the measured problem) |
| **Enforced by** | `tests/unit/IdleXIdle.Game.Tests/action_presentation_test.cs`, `action_handoff_test.cs`, `melee_action_test.cs`, `jaws_reaction_test.cs`, `press_field_test.cs`, `brand_mark_test.cs`; `tests/unit/IdleXIdle.Core.Tests/Builds/reaction_armed_test.cs`, `brand_marked_report_test.cs` |
| **Scope** | champion actions that have a recipe in `ActionRecipes` AND a timing file beside their strip. Today: the Seeker's `projectile` Form with its `projectile` effect (SPRAY), and the skill HARD HANDS (`sig_seeker_hard_hands`, its own recipe and strip; BLOW and PRESS are not performed). The action handoff applies to every committed champion clip. REACTIONS with a recipe in `ReactionRecipes` (today the Seeker's JAWS, `snare_jaws`) are presented on their own layer and are never a committed clip. FIELDS with a recipe in `FieldRecipes` (today the Seeker's PRESS, `hammer_press`) are presented on the field layer from the fight's Aura / Break events, and are never a committed clip either. MARKS with a recipe in `MarkRecipes` (today the Seeker's BRAND, `sign_brand`) are drawn ON the marked creature's body in the creature loop, built from the fight's Aura ticks, `Marked` depths and the falls the screen shows. |

## Summary

The fight still resolves every hit at its beat, and nothing about that changes. What changes is the presentation around the beat:

- **Contact on the beat.** A recipe makes the champion's clip start early enough that the thrown object leaves the hand one flight before the beat. It then lands **on** the beat, on the same frame as the fight's own hit feedback: health, flash, number and death.
- **Authored frame timing.** The clip plays by its own timing file: per-frame durations, named markers, and hand sockets.
- **Sockets.** A socket is a point on a frame, turned into an arena point with the same transform that draws the sprite.
- **The same object in the hand and in the air.** The held prop and the flying object are one texture, drawn at one scale.

## The reference implementation: SPRAY

Every future authored combat action is judged against the Seeker's SPRAY. It is the reference for:

| Concern | Where SPRAY does it |
|---|---|
| Anticipation before the deterministic combat beat | `ActionPerformance.Schedule`: the clip starts `travel + time-to-release` before the beat; elastic frames absorb TEMPO |
| Release markers | `<strip>.clip.json` `markers` (`anticipation`, `commit`, `release`, `recovery`, `settle`) |
| Per-frame authored timing | `ActionClipTiming` (90 110 150 50 230 90 90 110 ms) |
| Release sockets | `ActionSocket` / `ActorSocketMap` (`ThrowHand` on frames 1–3) |
| Projectile fan presentation | one knife per struck enemy, fanned from the held bundle, bowed apart (`FanBulge`) |
| Contact on the beat | `ActionPerformance`: release = beat − 250 ms, contact = the beat, same frame as the fight's events |
| Synchronized target feedback | health, flash (per-recipe envelope), number, death and contact sound on the contact frame |
| Material vs Source colour | steel untinted (AlphaBlend); edge, trail, glint and contact are Source light (`VfxBlend.Light`) |
| Impact priority | the performed hit replaces the generic puff and thud; the flash and number stay |
| Semantic audio timing | release cue at the release, ONE contact cue + ≤2 outer ticks, others ducked (`SoundBank.Duck`, `lead`) |
| Action handoff / recovery compression | `ActionHandoff` + `ActionClipTiming.FitRecovery`: arrive at the exit pose, never cut |

Its three sounds are HUMAN-APPROVED (2026-09-25): `sfx_seeker_spray_release`, `sfx_seeker_spray_hit`, `sfx_seeker_spray_tick`.

## The melee reference: HARD HANDS (ACCEPTED 2026-09-25)

HARD HANDS' visual reference and its timing reference are ACCEPTED, and its two sounds are HUMAN-LISTENED AND
ACCEPTED (`sfx_seeker_hard_hands_commit`, `sfx_seeker_hard_hands_hit`). The skill-specific recipe isolation below is
an accepted CONTRACT for every later recipe.

The Seeker's HARD HANDS is the reference for MELEE / DIRECT CONTACT: a leaping overhand HAMMER-FIST. He draws his
fist up, bounds in, drives it down onto the creature with his whole weight on the beat, follows through low, and
retreats home in guard. The owner approved the choreography (the verb, the held fist, the contact on the beat, the
root motion to the target, the impact, the exit pose, the handoff and the fast-TEMPO yield); the second pass
(2026-09-25) gave the recipe to the SKILL, rebuilt the way home as a retreat and fixed the cloak.

| Concern | Where HARD HANDS does it |
|---|---|
| One physical verb in five phases | key poses: ready, anticipation, loaded (held), COMMIT (the leap), CONTACT, follow-through (lowest), RETREAT (a guarded backstep), exit = the idle's first frame |
| A signature action belongs to its skill | `ActionRecipes.For`: the skill's own recipe (`sig_seeker_hard_hands`) first; its own strip (`seeker_hard_hands`), so BLOW keeps the Strike Form's |
| Contact on the beat | `MeleeActionRecipe`: the anchor is the `contact` marker with no flight (`TravelMs` 0), so the clip's contact frame IS the beat |
| Closing the distance | `MeleePerformance.RootMotion`: presentation root motion (pull-back, an accelerating commit, overshoot), the reach measured from the fist's socket on the contact frame to the target |
| Explosive approach, controlled withdrawal | the retreat: the follow-through stays planted, then the body leaves slowly, is fastest mid-way and settles home (`Retreat`, `ReturnEase`), ~234 ms at normal TEMPO with a 7 px low bound (`RootLift`); 110 ms at the constrained floor |
| Per-frame timing | 70 100 90 120 80 90 225 105 ms; contact 380 ms in; commit, contact and follow-through rigid, the retreat and the exit elastic |
| Impact follows force | a compressed white-hot flash, a soft shock ring squashed along the force, light chips, dark slivers, sparks, all starting AT the fist on the beat |
| Impact priority | the performed blow replaces the generic strike strip, puff, `sfx_cast` and `sfx_hit` |
| Semantic audio | `sfx_seeker_hard_hands_commit` at the leap (beat − 120 ms), `sfx_seeker_hard_hands_hit` on the beat; human-listened and accepted (2026-09-25) |
| Exit pose | the idle's own frame 0, reached through the retreat: its joins into idle are sub-pixel |
| One garment | the cloak's colours remapped to the idle cloak's own distribution on the flared frames (`keyposes.py` `recolour_cloak`) |
| Handoff at the fastest TEMPO | `HandoffFit.Yielded`: it hands over after its contact, and the lunge is carried home under the next wind-up |

## The reaction reference: JAWS (ACCEPTED 2026-09-28: the GOLD-STANDARD REACTION reference, the frontal smoke-teeth Shadow bite and its real-foley bite cue; the bear trap, the piranha and the side-view maw were NOT approved and are history)

**ACCEPTED: the gold-standard REACTION reference (the owner, 2026-09-28).** "A is perfect, exactly what I wanted. We can close JAWS with this sound effect and mark it as a gold standard." JAWS is CLOSED. Beside
SPRAY (PROJECTILE / TRAVEL) and HARD HANDS (MELEE / DIRECT CONTACT), it is the reference a future REACTION follows:
- **Architecture:** a skill-id recipe (`ReactionRecipes.For`), its own layer (`ReactionPerformance`, never the champion's
  body), the Core `ReactionArmed` truth for the dock, the real reflected target, isolation from other skills, deaths and
  the number ordered after the snap; alloc 0.
- **Picture:** the effect on the ATTACKER (frontal, on its head); one material phenomenon (mist gathers behind the
  creature, the teeth condense from smoke, compress, snap, burst, evaporate); SMOKE teeth with the snap frame alone
  condensed solid and white-hot; the burst's hierarchy (fangs + hot core, then the ring, then splinters + mist, speed
  lines an accent); quiet (no white-hot, a dimmer flash, no speed lines) while the champion performs and on a second
  creature, so a reaction never outshines the action; ~677 ms.
- **Sound:** ONE cue on the snap, `sfx_seeker_jaws_bite`, built from REAL recorded foley (`make_jaws_bite.py`, CC0
  sources in `tools/asset-pipeline/foley/jaws_bite/`) and shaped to a MEASURED reference the owner named; ducked 0.45
  under an action. The approved bytes are pinned by their SHA-256 in `jaws_reaction_test.cs`: changing the cue is a
  new approval, never a regeneration.
- **The method that got here:** an owner-named reference is fetched and measured (analysis only; a third party's audio
  or art is never sampled, shipped or published), the build is matched to the measurement, and a measurement QA stands
  in for the ear that nobody on the build side has.

**The cue is a REAL bite (the owner, 2026-09-28).** The owner rejected the three synthesised bites: "all similar; I
meant a real bite, like the sound when Trundle bites with his Q in League of Legends." That reference was measured
locally (analysis only; Riot's audio is never sampled, shipped or committed): one dense ~500 ms block, a chewing tear
first (4-5 bursts), a pitch-dropping sub second (the loudest band), wet resonances, cartilage ticks; 47 % of the energy
below 150 Hz and ~32 % above 2 kHz (the synthesised bites: 77 % and 1.6 %, a thud). `sfx_seeker_jaws_bite` is now built
by `tools/asset-pipeline/make_jaws_bite.py` from 14 CC0 recordings (`tools/asset-pipeline/foley/jaws_bite/SOURCES.md`);
only the sub and the wet resonances are synthesised; A matches the reference within ±5 dB per band and 50 ms almost
everywhere. Candidates A CHOMP (in the game), B BONE, C JUICY. A two-lens measurement QA found and the build fixed
aliasing, true-peak overshoot, dead-cut subs, splice clicks, a thrice-played transient, an "808" sine, empty mids, ticks
on silence and a late onset. The rejected cues moved to `tools/asset-pipeline/audio_history/`. Evidence
`production/qa/evidence/jaws-real-bite/`; review page https://claude.ai/artifact/6tP43HjDMCtfKGWeHzHCRu. The owner chose A: APPROVED.

**The picture APPROVED; the cue made a BITE (the owner, 2026-09-28).** The owner on the smoke teeth: "This version is
very nice, I approve it. Finally, if you make the sound effect more of a bite / being-bitten sound, we approve this
skill completely." The picture is APPROVED. The one cue on the snap is now `sfx_seeker_jaws_bite`
(`make_action_sfx.py` `_bite`): the teeth MEET (two enamel clacks 4 ms apart), SINK IN (granular crunch grains and a
short wet squish), the JAW'S WEIGHT (a short low thud), a whisper of Shadow; one bite, every layer inside the first
~50 ms. Three candidates: A CHOMP (in the game), B CRUNCH, C HEAVY, rendered into the same film from the trace. The
thorn impact `sfx_seeker_jaws_fangs` is history. Traced on the snap (+316 ms) in all four takes, ducked 0.45 under an
action. Evidence `production/qa/evidence/jaws-bite-sound/`; review page https://claude.ai/artifact/UyNseCpfkpYrHioX4P5ucX. Awaiting the owner's ear:
the chosen candidate → JAWS ACCEPTED.

**Smoke teeth (the owner, 2026-09-28, on the shadow-mist polish): the build.** The owner: "The effect is very good; I
just want it a little more like smoke, more like mist, with a little lower opacity."
- **The teeth are SMOKE held in a tooth's shape** (`seeker_bite.py` `smoky_row`): round, curling billows of density
  inside each tooth (tall vertical licks read as purple flame and were rounded), a soft wavering outline that hardens
  toward the point (the points stay points), roots dissolving into smoke, short soft wisps off the gum, no glass rim.
  The six condensation states derive from it. The solid rows are in `keypose_sources/history/*_solid.png`.
- **The SNAP cell** (`ReactionRecipe.SnapCell`, a seventh cell; `ReactionPerformance.CellAt`): on the first frame drawn
  at the snap only, the smoke condenses HARD into the solid teeth (white-hot unless the champion performs), then breaks
  back as smoke. Drawn as smoke, the snap was a star over a haze and the jaw was never seen to shut (a judge rejected
  it); now the snap frame is pixel-identical to the approved one.
- **Opacity:** formed 0.75 → 0.62, snap 1.0 → `SnapOpacity` 0.88; the smoke's own density thins it further (a first
  try at 0.55 / 0.85 made the pale early phase too faint).
- **Checked:** two judges, then two adversarial re-checks: ACCEPT WITH NOTES, no defect. Geometry (the spans file),
  timing, colours, the burst, the mist, quiet mode, the one cue and the number are unchanged; alloc 0; 808 game and
  1888 Core tests. Evidence `production/qa/evidence/jaws-smoke-teeth/`; review page https://claude.ai/artifact/PGbMWLcVTPtJzxaeDj2Jr1.

**The shadow-mist polish (the owner's brief "JAWS Frontal Bite Final Shadow-Mist Polish", 2026-09-28): the build.**
The frontal bite is APPROVED ("Do NOT redesign JAWS again"); the brief asked for visual polish only, so that the bite
reads as ONE Shadow phenomenon: mist gathers, the fangs condense, the Shadow compresses, snap, the Shadow bursts
outward, the teeth break back into mist, everything evaporates. The perspective, the fang geometry, the head placement,
the charge colours, the snap timing, the reflected target, `ReactionArmed`, skill-id isolation, SPRAY, HARD HANDS and
Core are unchanged.

- **One Shadow mist with one lifecycle** (`ReactionPerformance.Mist`, pure and tested; `ReactionPerformance.DrawUnder`).
  Three soft irregular sprites of one lobe (`fxp_seeker_bite_smoke`, a broad soft body with a torn edge), flipped and
  never tilted: no emitter, no allocations. Each lobe is drawn at `StackedShare`, the share that stacks to the recipe's
  opacity. Opacity 0.07 at spawn, 0.38 formed (100 ms), 0.45 at the snap; scale 1.12 to 1.0 while it forms, compressed
  to 0.92 at the snap with its lobes drawn in (the charge is form and density, not a pulse), stretched a little by the
  rows' own gap in the wind-up, then released slowly to 1.22 while it fades to 0. It never fades in twice. Core colour
  near-black violet (12, 7, 20) with a subtle violet edge on one lobe. It is drawn BEFORE the creatures (after the
  effects' own under-pass), 2.9 × 2.7 crown widths, so it frames the bitten creature and can never veil it or the teeth.
  Two findings on the way: the first pass drew it over the creature, in front of the bitten body (the review moved it
  behind); drawn behind at the jaw's size, the body hid it completely, and a "dark" violet as light as the floor added
  back what it took away. At
  the brief's opacities it is a restrained dark haze around the bite, on purpose (the brief: reduce, never compensate).
- **White-hot for one frame** (`_hotU`, the first frame DRAWN at or after the snap); the next frame is magenta. The
  flash is 140 ms with the two-frame peak kept and cools white → magenta → violet (`FlashTint`, `CoolColor`); after the
  snap nothing moves toward brighter pink.
- **Hierarchy.** PRIMARY the snap fangs and the hot core; SECONDARY the pressure ring (a crisp edge in the light pass
  hands over to a softened copy under the champion that dissolves into the mist, `RingCrispMs` 200 of `RingMs` 300);
  TERTIARY the splinters (pale magenta, then darker violet and softer: a two-cell part, crisp and soft, `ShardTint`)
  and the mist; ACCENT 13 speed lines (`STREAK_COUNT`, the count drawn is the count written), gone by 70 ms.
- **The slash is removed.** A directional stroke inside a radial burst read as a BLADE attack in true-speed review;
  JAWS is a bite. The part moved to `keypose_sources/history/fxp_seeker_bite_slash.png`; seven runtime parts remain.
- **Quiet mode** (`IReactionStage.ChampionPerforming`; HuntScreen: an action performance is live). While the champion
  performs SPRAY or HARD HANDS, and on a second bitten creature, the snap is never white-hot, the flash is ×0.55 alpha
  and ×0.8 size (`QuietFlashAlpha`, `QuietFlashSize`), and there are no speed lines; a second creature also has no ring.
  JAWS is never brighter or larger than the champion's action.
- **Duration** ~677 ms, unchanged; the tail is faint mist and the last splinters and does not persist, so no shorter
  tail variant was made. One cue, on the snap; no whoosh, no charge sound.
- **Checked** by three independent judges (readability; one phenomenon; hierarchy and context) against the current
  version at every phase: all three ACCEPT WITH NOTES, no defect. The bite is exactly as clear up to the snap; one peak,
  no second fade-in; the palette cools; nothing veils the creature or the teeth; under SPRAY the knives stay brighter
  and larger. Their shared note: the mist is at the edge of perception at play speed (its core sits behind the black
  body), and making it stronger would move toward a veil. Their other notes, kept as levers rather than changes: the
  translucent condensing teeth read ~5 % darker over the mist (0–150 ms); under HARD HANDS the flash and ring (light
  pass) paint magenta over the champion's fist for ~130 ms, as before but dimmer (the lever: draw them under the
  champion in quiet mode).
- **Proof:** alloc 0 on every traced frame; at most 20 sprites for a two-creature bite; 808 game and 1888 Core tests;
  the asset gate. Evidence: `production/qa/evidence/jaws-bite-mist/`; review page https://claude.ai/artifact/7UWmmu6T3gPhb59YZSnQ5H.

**A frontal Shadow bite (the owner, 2026-09-28, third review): the build.** The owner on the side-view maw: "No, you
misunderstood. You drew a bad jaw biting from the side. What I meant was the earlier perspective, biting from the FRONT,
but a more effective and beautiful jaw", with a reference: Roni Kangaskorte's "Bite VFX" on ArtStation (an upper crown
of fangs and a lower row appear apart, charge colour, slam together, and burst into a flash, a slash, a ring, speed
lines and shards). JAWS is now that bite in the Shadow palette:

- **Eight parts, composed at runtime** (`tools/asset-pipeline/v2/seeker_bite.py`, procedural, every random draw from one
  SEED, no PixelLab; `keypose_sources/seeker_bite_spans.json` pins the cells, the rows' bite lines and every tooth's
  point, which the tests hold the recipe to): `fxp_seeker_bite_upper` (the CROWN: two long canines at its corners that
  bow out and hook their points in, "( )", five packed leaf-shaped teeth between them, each overlapping the next, their
  points on one flat line) and `fxp_seeker_bite_lower` (four leaf-shaped teeth whose points rise between the upper
  points, and a taller one at each end that the canines close outside of), each in six mist-to-crisp states; and the
  impact's `_star` (a small hot core, six fat rays), `_slash` (one tapered stroke), `_ring`, `_streaks`, `_shards` (torn
  splinters) and `_smoke`. All are white or grey and tinted by the recipe. Straight cones read as crystal spikes; the
  teeth are LEAVES with feathered edges, a soft halo and a thin seam where one overlaps the next.
- **Where** (`ReactionPerformance.Fangs`, pure and tested): on the FRONT of the creature's canonical body (0.30 of its
  width from the left, where its head is: every creature faces the Seeker; centred on the whole body, the bite fell
  between two heads of a pack), the crown 0.45 of that width, the rows meeting at 0.52 of the drawn silhouette's height,
  open 0.62 of that height apart (the crown under the health bar, the row above the skill dock). A second creature
  bitten by the same answer gets its own bite, 0.68 the size, on its own head, snapping at the same moment (a later
  second bite let its creature fall and its number show before its own rows shut), its impact only a flash and
  splinters; the one cue is the front bite's (`FrontTarget`; the fight's target order is not front to back).
- **The phrase**, in ms after the first frame that shows the bite: 0–100 the rows condense out of mist over six frames
  (in one frame it read as a pop) to 80 % strength, pale lavender-grey; they CHARGE through violet to magenta and
  strengthen to whole by the snap; 150–233 they part a quarter further (the wind-up); 233–317 they slam together,
  accelerating (the largest step on the snap frame); SNAP at 316.6 (frame 19; the cue and a kill's fall): interlocked,
  white-hot, the brightest moment; they break into the IMPACT over the next two frames: the flash swells in one frame,
  holds two, then SHRINKS to a hot point before it fades (a large fading star read as a grey sticker); a magenta slash,
  longer than the flash, strikes across it at +35° between the star's rays (along them, the star swallowed it); the ring
  grows to about 1.4× the crown's width while still bright; the speed lines reach about 3.4× its width; the splinters
  fly out with the ring and are the LAST to fade; a violet haze spreads behind; gone at 677. The number 40 ms after the snap (on screen ~50). Every part fades in steps (tested on the 60 fps
  frame grid).
- **Layers:** the teeth, their glow (the same states drawn again with a ZERO alpha, so in the premultiplied batch they
  only add light), the splinters and the haze are drawn after the creatures and BEFORE the champion (in HARD HANDS the
  Seeker stands in front of the teeth; drawn in the additive pass, the glowing teeth landed on top of her); the flash,
  the slash, the ring and the speed lines are light (the shared additive pass). At most 10 sprites a target, alloc 0.
- **Verified** by two judge rounds and a confirmation round against the reference frames (close and gameplay scale
  plus an adversarial code review); their findings (the teeth as crystal spikes, the bite between two heads, the fangs'
  appearance brighter than the snap, a one-frame condensation, an invisible wind-up, an impact smaller than the jaw, no
  slash, splinters that faded early, a dark puff lost on the pack, a fused double crown for two targets, the glow over
  the champion, a mistimed break, a dead seed, opposite tip curves; then the slash hidden under the star's rays, the
  flash's grey tail, a ring inside the crown while bright, a skipped condensation state, a glow that whitened the formed
  teeth, a delayed second bite whose creature fell before its rows shut) were each fixed and, where a test can hold
  them, pinned by a test.
- **Unchanged:** the skill-id recipe, the real reflected target, `ReactionArmed`, no champion-body ownership, REPAY
  isolation, death ordering, the SPRAY / HARD HANDS overlaps, the one cue, no flash of the creature's own sprite, the
  number after the snap. Evidence: `production/qa/evidence/jaws-bite/`; review page https://claude.ai/artifact/4a9WdmEBai3Q4bCWkNZaqP.

The side-view maw it replaced is recorded below.

**A Shadow maw made of mist (the owner, 2026-09-28, second review): the build.** The owner on the misty teeth:
"definitely better, but the bite still needs to read more clearly; the overall duration can be a little longer, or the
first bite moment more pronounced; and the teeth should not be four triangles coming to the middle: really a jaw that
opens and closes (it can be a piranha jaw), a bite effect". JAWS is now a SIDE-VIEW MAW, still made of mist:

- **Two jaw pieces in sixteen condensation states** (`fxp_seeker_maw`, 16 × 320 × 176 per row, the upper jaw above the
  lower; `tools/asset-pipeline/v2/seeker_maw.py`, procedural, one fixed noise field per piece, no PixelLab; it writes
  `keypose_sources/seeker_maw_spans.json`: cell, hinge, jaw lengths, every tooth tip and the gum curve, which the tests
  hold the recipe to). Each jaw is a tapering jaw with a rounded cheek at its hinge, a convex brow and a BLUNT snout (a
  needle point read as a beak), five GRADED teeth on its front (the front fang about 1.7× the back tooth, raked back; a
  row of equal teeth read as a zipper), the lower jaw 13 % longer (a piranha's underbite) with its teeth half a tooth
  along in absolute px, so the rows truly interlock. Material: a dark violet Shadow MASS with smoke moving inside it (a
  lightening lavender fill read as glass; a thin darkening veil vanished on the dark floor), a soft outline broken by the
  smoke and fading out toward the hinge (the back of the jaw is a ragged plume, never a closed capsule), smoky lavender
  teeth that HARDEN and catch the light only in the last states, which the runtime shows only on the bite. ONE state per
  piece per frame. Chosen from three designs (piranha / crocodile / beast) by a three-judge panel on composites over a
  real frame; unanimous: piranha. The jaw's OUTER side stays smoke (softer and more ragged than its crisp biting
  edge), and a dark LIP runs along each gum with the lower teeth a shade darker than the upper, so the shut mouth reads
  as two jaws meeting (one zigzag strip of equal teeth read as a zipper).
- **The maw** (`ReactionPerformance.Maw`, pure and tested) turns both pieces about the hinge. It is anchored
  HORIZONTALLY to the creature's CANONICAL body (a lunge is drawn on a canvas far ahead of the creature; anchored to it,
  the jaw shut on the empty floor once the creature stood back up), the hinge 0.30 of the jaw's length in front of it
  (the jaw ~0.58 of the body's width, about 70 % of it over the creature); VERTICALLY the shut seam crosses the DRAWN
  silhouette at 0.60 of its height, 0.65 of the way along the jaw where the teeth close (`BiteSeamShare`, held to the
  art's tooth rows): the THROAT, under the head (shut just under the eyes, the band of teeth read as the creature's own
  grin). The whole maw leans 4° snout-down. When one answer bites several
  creatures, the FRONT one (nearest the Seeker, `FrontTarget`; the fight's target order is not front to back) gets the
  full maw and every other its own maw, 0.85 the size, formed in place at its own front edge (a full maw reached back
  across the creature in front of it). It is drawn over the creatures and UNDER the champion: in HARD HANDS the Seeker
  stands in front of the bitten creature, and a maw drawn over her lay across her fists like a serrated weapon.
- **The phrase**, in ms after the first frame that shows the bite (longer, as the owner allowed): 0–83 loose smoke
  gathers into a nearly shut jaw that comes in from the Seeker's side and WAITS, nearly shut, to 133 (`OpenFromMs`: the
  biting creature's own lunge drawing is up until ~120–150, and a jaw opened over it, violet on violet, faded in already
  wide); 133–200 it visibly OPENS WIDE (−45° / +30°: the upper jaw rises clear of the creature's eyes, so the face sits
  INSIDE the open mouth; with the upper teeth across its eyes the open jaw read as the creature's own grin), condensing
  until its teeth are clearly seen; holds wide to 233; 233–300 it closes, accelerating (an ease-in of power 3: two frames
  part-way, the LARGEST step, 58 % of the travel, on the snap frame itself, with the sound) and hardens; SNAP at 299.9
  (frame 18, a hair under it so the playhead lands on the shut pose; the cue and a kill's fall): the jaws bite PAST their rest to ±8.5°, where the tooth tips just reach the other jaw's gum line
  (never through it: shut to a line, the rows looked inside-out), held two frames, the teeth catching the light; the
  CLENCH: ONE 16 px push along the jaw into the creature, part-way on the snap frame and whole on the next, then easing
  back in a straight line to nothing at `ClenchMs` (at its peak on the snap frame and backing off at once, it read as a
  bounce; an in-out-in jolt buzzed), and a 16 % swell on the snap frame and the next, easing back in even steps over 66 ms; it settles to a
  slightly open REST (±10.5°: two wedges, the creature between them, the teeth interlocked about half their length) and
  tightens over three frames (with one frame, the tighten was an in-out flicker); the number 40 ms after the snap;
  release at 433 (a 133 ms hold); DISSOLVE 433–593 (`DissolveMs`, its own length from the release, so a retuned hold
  never squeezes it), the jaw lets go and opens (to −22° / +24°), comes apart into smoke (one or two strip states a
  frame), rises 28 px, fades in even steps. Every segment after the snap is clamped to the release (a test sweeps the
  hold over 80–200 ms and finds no jump). Two sprites per target, alloc 0.
- **Verified** by four judge rounds (vision judges on true-scale and gameplay-scale frames plus an adversarial code
  review each). The second round's defects (rows inverting at the snap, the jolt's buzz, an uneven swell, a jaw mostly
  shut before the snap frame, the jaw over the eyes, a glassy fill, a six-state jump in the condensation, the rear maw
  across the front creature) and the third round's (the open jaw read as the creature's grin, the opening hidden behind
  the lunge drawing, a bite frame that was not the peak, a clench that backed off at once, a zipper-like shut band, a
  glassy capsule, the maw over the Seeker in HARD HANDS, the rear maw arriving over the front creature, a one-frame
  tighten, an untested front-target choice) and the fourth's (the shut band just under the eyes read as the creature's
  grin, the opening still under the lunge drawing, the zipper, a stale layer order in the docs, an absolute dissolve end,
  a hard-coded jolt table) were each fixed and are each pinned by a test where a test can hold them.
- **Unchanged:** the skill-id recipe, the real reflected target, `ReactionArmed`, no champion-body ownership, REPAY
  isolation, death ordering, the SPRAY / HARD HANDS overlaps, the one cue, no JAWS flash, the number after the snap (a
  critical answer also plays the game's general critical cue with its number, as every skill's critical does).
  Evidence: `production/qa/evidence/jaws-maw/`; review page https://claude.ai/artifact/5cemkjDpzbxWn28P4YmS5Z.

The misty-teeth build it replaced is recorded below.

**Shadow fangs made of mist, that close (the owner, 2026-09-28): the build.** The owner's review of the Shadow Fangs
+ mist pass: "the teeth don't close; and what I meant was that the teeth themselves are like mist: instead of
appearing at once, they come like mist, bite, and vanish". The four teeth stopped on the creature's outline and never
met, and they were hard sprites with a separate fog. Now the TEETH ARE THE MIST and the JAWS SHUT:

- **One misty fang in sixteen condensation states** (`fxp_seeker_mistfang`, 16 × 144 × 208, the point at (72, 178);
  `tools/asset-pipeline/v2/seeker_mist_fangs.py`, procedural from the approved fang's outline, one fixed noise field,
  no PixelLab): state 0 is a loose drift of violet smoke wider than the tooth, with stray wisps; each state tightens
  the field toward the tooth, sharpens its edge and brings up the violet rim and a pale-violet point; state 15 is the
  condensed tooth (a dark violet, its inside a little translucent so a creature's eye under it still shows, its rim
  running most of its length), still smoky at its root. ONE state is drawn per frame (two cross-faded "over" draws
  thinned the tooth between states). The separate floor mist of 2026-09-27 is gone; it and every other unused JAWS part
  moved to `tools/asset-pipeline/v2/keypose_sources/history/` (kept as history, not loaded at boot).
- **A jaw that closes** (`ReactionPerformance.ToothPoses`, pure and tested): two upper teeth (points down) and two
  lower teeth (points up, 0.8 of the upper's size), the rows interlocking like a shut mouth (upper pair 0.46, lower pair
  0.26 of a tooth either side of the centre line, each point leaning 8° inward). The open jaw forms ON the creature's
  body (the upper points 0.20 of its height inside its top, the lower points 0.25 inside its bottom), so the mist stays
  below the enemy's health bar and above the skill dock. At the snap both rows' points reach 0.67 / 0.53 of the height
  (meet 0.60 + overlap 0.07): the upper points end below the lower ones, the teeth CLOSED across the creature's LOWER
  middle, off its face and eyes.
- **The phrase**, in ms after the first frame that shows the bite: GATHER 0–65, the mist grows out of nothing (opacity
  0.10 → ~0.3 → ~0.6 → ~0.9 → whole, an ease-in-out over 60 ms) as four violet plumes that come IN from the sides (2.4×
  wider apart, 1.25× larger, an ease-in-out arrival) and condense slowly (an ease-in, so the loose smoke is what is
  seen); CLOSE 65–130, the jaws shut, accelerating (power 2), three 60 fps frames part-way, each step larger than the
  last; SNAP 130 (133 on screen), the cue and a kill's fall; BITE 130–190, held; the number at 170; DISSOLVE 190–300, the
  rows part 12 px, the teeth come apart back into smoke over the first 60 % of it (four frames), the smoke RISES 26 px
  and spreads (+45 %), fading evenly (1 − k^1.25, the last frame faint); nothing at 300. Four sprites per target, alloc
  0. Three verification rounds (judge panels + a code reviewer) drove these numbers.
- **Unchanged:** the skill-id recipe, the real reflected target, `ReactionArmed`, no champion-body ownership, REPAY
  isolation, death ordering, the SPRAY / HARD HANDS overlaps, the one fang cue, no JAWS flash, the number after the
  snap. Evidence: `production/qa/evidence/jaws-mistjaw/`; review page https://claude.ai/artifact/8trdfMEnhTAoZTtUgg1dFD.

The Shadow Fangs build of 2026-09-27 that this replaced is recorded below.

**The final direction (the owner, 2026-09-27).** JAWS belongs to the same semantic family as reactive / thorns-style
damage, and its presentation is intentionally SIMPLE: ENEMY HITS SEEKER → LARGE SHADOW FANGS SNAP ONTO THE ATTACKER →
JAWS DAMAGE → FANGS RELEASE → GONE. No literal creature needs to be identified. The piranha before it (three jaws, then
a hero chomp, then one piranha staged in negative space) was rejected not for its timing but for its COMPLEXITY: at
gameplay scale a tiny creature carrying a head silhouette, an eye, a mouth, teeth and body/tail detail is below the
useful perceptual budget, and at true speed it becomes coloured motion. What survives reduction is four strong
triangles. What it is, in the build:

- **One hand-authored fang** (`fxp_seeker_fang`, a 40 × 72 canvas, the point at (22, 71); `tools/asset-pipeline/v2/seeker_fangs.py`,
  drawn deterministically at 4× and downsampled, no PixelLab): a broad, slightly curved triangle, a near-black violet
  Shadow body, a strong violet edge, a small pale tip. The runtime draws it FOUR times: two from above with their points
  down, two from below (the same sprite flipped vertically), each leaning ~12° toward the body's centre. Every fang is
  the same material; the geometry is controlled. No piranha body, no eyes, no tails, no separate head, no glint, no
  residue, no secondary bites, no particles, no trails.
- **Large**: each fang is ~34 % of the creature's visible height (28–72 px); the upper and lower fangs together are
  ~68 % of it. Large SIMPLE geometry is cheaper to read than small detailed geometry.
- **The phrase** (`ReactionPerformance`), in ms after the first frame that shows the bite: at 0 the fangs appear OPEN,
  their points ~12 % of the body's height OUTSIDE its top and bottom edges, so there is visible empty space between the
  upper fangs, the body and the lower fangs: the first thing the eye sees is something surrounding the creature
  (nothing begins inside it). The open pose stands to 25, then the fangs close RAPIDLY inward (an ease-in: upper down,
  lower up) and SNAP at 55 onto the creature's outer silhouette, the points ~16 % inside its edges, the body still
  visibly between them (they bit its outline, they did not cross through it). They HOLD there 85 ms, the semantic
  pose: several frames of TARGET BETWEEN SHADOW FANGS, which is where the readability comes from. Then a 6 px outward
  release and a quick fade, gone by 200. Foreground only: no behind layer, no masks (readability beats physical
  accuracy).
- **The Shadow Mist (the polish pass, 2026-09-27; the direction approved, "do NOT redesign the effect").** ONE
  restrained supportive layer so the phrase reads as "Shadow gathers around the creature, jaws snap onto it, then the
  darkness evaporates" instead of four sprites moving inward. Exactly two layers, mist and fangs; still no particles,
  glints, trails, flash or bloom. Two procedural wisps (`fxp_seeker_mist_a` / `_b`, `tools/asset-pipeline/v2/seeker_mist.py`,
  no PixelLab): near-black (darker than the creatures' own black) with a very restrained violet fringe, a compact core
  and a long feathered fringe, zero alpha on every canvas edge; the art script records what it measured
  (`keypose_sources/seeker_mist_spans.json`) and the tests pin the recipe against that. The pocket is centred on the
  bitten creature's drawn silhouette and drawn in the normal alpha pass **UNDER the figures** (`ReactionPerformance.DrawUnder`,
  called before any creature or boss is drawn): a Shadow pool the creature stands in, never a veil over it. Chosen by
  measurement and a three-judge panel: drawn OVER the creatures the same fog greyed the bitten whelp's white eyes by
  ~40 %, a thin veil over them by ~11 %; UNDER, its eyes and rim are exactly as without the mist, and a second round
  (the readability refuter's measure) had it cut further: the stacked density capped near 0.6, the pocket raised onto
  the torso and back (off the floor between the feet) and sized to ONE creature (the narrower of the drawn silhouette
  and the canonical body: a lunge is drawn on a canvas twice the creature's width; the height is the body's own, since
  the fangs sit on its edges); a third round lightened the release by a third (the frames where the darkened floor beside
  the pack's black art cost the most) and made the mist's evaporation linear (no stall at the release, then collapse),
  with a darker, less violet colour. The bitten whelp keeps ~90 % of its silhouette separation from the floor at the snap
  and 95-98 % through the release. The curves (the layer's opacity; the main
  pocket's densest texel is x0.72 of it on screen): a breath (0.06) on the first frame so the fangs never appear on bare
  floor, ~0.28 at 15 ms, ~0.47 at 30, 0.70 at the snap (~0.50 on screen, ~0.59 where the wisps overlap); it CONTRACTS
  onto the creature as the fangs close (open ~1.16 of its snap size); dense and compressed 35 ms after the snap, then it
  loosens outward along ONE smooth curve (25 % by the end, ~11 % of it from the release to the last frame that shows it),
  thins to 0.42 through the hold and evaporates linearly to nothing at 200. The fangs now EMERGE: 0.30 on the first frame, 0.70 when the open pose ends, whole and crisp
  at the snap, whole through the hold, then 1 -> 0 over 45 ms, faster than the mist, and not drawn at all below
  `FangVisibleFloor`; the phrase stays 200 ms, and the frame after the fangs (+183) still shows a faint fog (~0.08).
  Four fang sprites + two mist sprites per target (all main pockets, then all second wisps); alloc 0 over both passes. Evidence: `production/qa/evidence/jaws-mist/`. Known and unchanged: on the review
  layout the lower fang pair's OPEN pose sits under the skill-dock band at the bottom of the arena (it rises into view
  at ~50 ms), in the approved fangs-only take as well.
- **NO JAWS TARGET FLASH.** The Shadow fangs ARE the reaction's hit feedback; the generic white F2 under them was the
  largest source of visual competition, so a presented reaction whose recipe has no flash flashes nothing
  (`ReactionRecipe.TargetFlash` 0). The global F2 for ordinary attacks is unchanged.
- **The number is delayed**: the reflected "−X JAWS" lands 40 ms AFTER the snap (`NumberDelayMs`; the trace's
  `reaction-number`), above the creature in the screen's usual number lane, clear of the fangs: SNAP → the player sees
  the effect → the number. A kill's fall lands on the snap. Core is untouched; this is presentation scheduling.
- **One cue**, `sfx_seeker_jaws_fangs`: a short, dark, dry, sharp Shadow bite / thorn impact (energy compression + a
  short organic impact), its transient exactly on the SNAP, played on that frame and never at the spawn. No secondary
  ticks, no metal trap, no swarm, no bone crunch; `design/audio/seeker-jaws-audio-brief.md`. The dry-steel
  `sfx_seeker_jaws_snap` and the piranha's `sfx_seeker_jaws_chomp` are history.
- **The target** is the actual reflected target from Core (the reflected Strikes at the bite's ms), each answered on
  its own body, riding it through its lunge, its bob and its fall; never the row's centre, never every enemy. The
  creature is not yanked; the Seeker does nothing (SPRAY, HARD HANDS and the basic swing are never interrupted).
- **The dock** keeps the Core-driven `ReactionArmed` rearm sweep. REPAY keeps its own legacy presentation (skill-id
  recipe resolution). The mechanical props, the chain parts, the N2 snap and the piranha states stay on disk as history
  and are not drawn.

The sections below record how the trap was reached and why it was rejected; they are history.

A REACTION takes no beat: the enemy's bite is its cause, and it must never take the champion's figure. The Seeker's
JAWS (`snare_jaws`, "every bite returns 50% of it to the enemy that bit you") is presented as BITE FOR BITE: the
creature bites the Seeker and the trap immediately bites it back. The owner approved the discovery's lifecycle, the
IRON JAWS identity (the skill's name, its icon, the bite-back fantasy) and one information-only Core event.

**What JAWS IS (the identity pass, 2026-09-25).** A SPRING-LOADED HUNTING CLAMP, a mantrap mechanism: "JAWS" names the
mechanism's two opposing clamp arms. It is never an animal head, a monster mouth, a mechanical dinosaur, a skull or a
biting robot. The polish pass's world prop hinged two long toothed plates that tapered to one tip on a round hub behind
them, and at true speed it read as a metal crocodile: a snout, two jaws and an eye. A blind reader named it "chomping
jaws / mandibles … the small dark circle is an eye". Its cue's crunch and thump read as bone. The identity pass
changed the SHAPE LANGUAGE and the TIMBRE.

**The readable-clamp pass (2026-09-25).** The identity pass overcorrected: a big riveted box with two thin quarter-arc
arms read as a MECHANICAL HOOK / GRABBER, and — the primary blocker — its close was NOT VISIBLE at true speed: 30 degrees
to the stop in 16 ms is one open frame and one shut frame. The pass rebuilt the world prop so the JAWS dominate and made
the close a motion the eye sees, keeping every approved system (Core readiness, target resolution, the layer, the
tether, the retract concept, dock truth, SPRAY, HARD HANDS, REPAY / NET / IRON untouched).

**The concept study (2026-09-25; nothing implemented).** The owner did not approve the bear trap and asked whether JAWS
wants a literal trap at all. The study's findings (`prototypes/jaws-concepts/README.md`,
`production/qa/evidence/jaws-concepts/`), in brief:
- **Deployed traps read because they are placed and armed BEFORE their trigger.** Examples: Dead Cells' Wolf Trap,
  Caitlyn's Yordle Snap Trap. JAWS has no placement, so a literal trap must explain itself inside the reaction; that
  is the physical burden.
- **Three stylised Shadow animatics (A jaws, B tether, C sigil) were blind-read.** None read as a COUNTER.
  - The control shows why: the enemy's bite reads as "a red burst on the hunter in the same frame".
  - The fight's own white hit flash erases whatever sits on the creature.
- **A line from the Seeker gives CAUSE.** Top-and-bottom jaws read as a MOUTH; a sideways "( )" close reads as
  "snapped shut around".
- **A 17–50 ms pre-roll is unseen when dim, and makes the Seeker the initiator when bright.**
- **The recommendation is a direction, not a decision.** A stylised sideways Shadow snap on the contact frame, one
  ownership line, and the trap's "before" moved into the armed state (`ReactionArmed`). The enemy bite's legibility and
  the answer's flash hierarchy come first.

**The foundation study (2026-09-26; nothing implemented).** The owner paused JAWS and asked for the two upstream
defects to be studied first: the enemy bite does not read as an attack, and the full-white hit flash erases what is
drawn on the struck creature (`prototypes/bite-readability/`, `production/qa/evidence/bite-readability/`). Findings:
- The Gloom Whelp's attack strip is a crouch in place; its contact frame equals the frame before it; its only fast
  motion is the recovery after the hit; the row's 40 px lunge starts ON the contact frame (a recoil, not an attack).
- The Seeker has no hurt reaction: a centred burst, no flinch, no direction. Every blind reader named this.
- Key poses + a restrained lunge (0.15-0.30 of the creature's width) did not read in blind flipbooks; the receiver's
  reaction and a distinct contact pose are the missing halves.
- The low shaped flash (peak 0.45, ~80 ms) keeps the target's body and eyes at the peak and still reads as a hit; the
  current full-white mask does not.
- On the improved baseline the JAWS concepts A and C read as automatic reactions (they read as casts before), but
  no reader saw the creature act, so the cause is still missing. Concept D is not yet justified.
- Every code change is a capture fixture (`RH_SHOT_NOTINT`, `RH_SHOT_SIL`, `RH_SHOT_NOANSWER`, `RH_SHOT_FLASH`,
  `RH_SHOT_BITE`, `RH_SHOT_HITSTOP`, `RH_SHOT_STRIP_FILES`), inert without its variable; awaiting the owner's word on
  keeping them.

| Concern | Where JAWS does it |
|---|---|
| The anchor is the enemy's CONTACT | the row's contact frame IS the `EnemyStrike` ms, on which Core resolves JAWS and its answer: the reaction starts in the pump, on that frame; no clock of its own (`ReactionPerformance` is a function of playhead minus the contact) |
| A layer, never the figure's owner | `ReactionPerformance` (not an `IActionPerformance`), in `HuntScreen._reactions`; the champion keeps his idle, swing, SPRAY or HARD HANDS; the old post-bite `trap` clip is skipped for a recipe'd reaction and not even loaded (`ActorClips`) |
| Belongs to its SKILL | `ReactionRecipes.For(character, SkillDef.Id)`: skill tier ONLY. JAWS and REPAY share the Snare Form's `trap` clip and effect, so there is no shared tier for traps; REPAY keeps its legacy presentation (filmed) |
| The lifecycle (readable-clamp pass) | ENEMY BITE (t 0: the bite's own thud and burst) → TETHER FIRES (the first frame that shows the bite: the OPEN trap is at the creature, a wide toothed cup; the chain whips out from his belt, a Source streak brightest at the trap; a tiny latch release in the ear; nothing flies: the trap is never a projectile) → JAWS SPRING SHUT (rotation only, the travel as the SQUARE of the time: still open at +17, about half shut at +33, a HARD STOP 3 degrees open at +50; ONE 4 degree rebound over 30 ms, locked) → THE ANSWER LANDS ON THE STOP (+50: the dry steel CLACK, the reflected number, the creature's soft flash, and a kill's fall) → CHAIN TAKES THE STRAIN (the whip's curve taut by 60 ms, a small shiver) → TARGET IS JERKED toward the Seeker (from the stop, a few pixels, peaking at +80, settled by +200) → TRAP RETRACTS (at 185 ms the jaws unlock and the trap is reeled along its chain into his belt, accelerating, fading only over its last 35 %) → gone at 285 ms. Its clock starts on the first frame that shows the bite, so the open jaws are always seen |
| The object: a bear trap whose JAWS dominate (readable-clamp pass) | a SMALL housing (a short forged base bar carrying the two pins, the trigger coil under it, the chain eye at its rear end) and TWO BROAD FORGED "( )" JAWS, each a semicircular band with three large teeth on its inner edge (interleaved with the other's, never a mirrored row): about 75 % of the silhouette. OPEN, the jaws lie spread in a wide toothed cup (34 degrees each: the space something is about to be caught in); SHUT, they stand "( )" round the limb with their tips apart at the top, where the limb comes out: steel jaw, TARGET, steel jaw; never a ring, a seam, a hook or a mouth. The identity pass's big box with two thin hooks read as a grabber; its predecessor's long head as a crocodile. Chosen from three play-size silhouettes through OPEN → LOCK: A, this upright bear trap (read blind as "a bear trap"; BEAR TRAP, CLAMP); B, an asymmetric mantrap along the chain (TRAP, CLAMP, but a sideways toothed opening reads as a mouth); C, an upright spring yoke (BEAR TRAP, CLAMP, "fang-like jaws on a stand") |
| Rigid, never scaled | the trap is three parts on one canvas (`prop_seeker_jaws_base`, `_near`, `_far`) drawn at ONE scale; each jaw ROTATES about its OWN pin (`NearPivot`, `FarPivot`: a compact pair 15 px apart on the base; the identity pass's pins stood 57 px apart at a plate's corners, a tall bracket, and the polish pass's one hub pivot was the "eye"); the base never turns against the trap; the trap stands upright and leans only a little toward its tether (half its angle, at most 12 degrees); the first build's whole-sprite pumping (0.82 → 1.12 overshoot) and its rise from the floor are gone |
| Layered round the limb | the NEAR jaw is drawn BEHIND the caught creature and the far jaw and the base in front: `ReactionPerformance.DrawBehind`, called by each creature draw (`DrawComposition`, `DrawNormalEnemy`, `DrawBoss`) just before that creature's sprite, in the same alpha batch; the material pass then skips it (a jaw whose creature is not drawn is drawn in front). Its glint is never drawn through the limb that hides it. The NEAR jaw, not the far one: it lies mostly outside the creature, so behind it loses only the part the limb crosses; the far jaw lies over the body, and drawn behind a dark creature it vanished (a blind reader saw "one jaw rotating upright, not two jaws snapping together"). No masking framework |
| The answer lands on the STOP (presentation scheduling only) | the fight resolves JAWS and its reflected blow at the bite (t 0) and nothing there moved. On screen, the answer's JAWS-specific feedback waits for the jaws' stop (~50 ms): the reflected damage number (and its critical cue), the creature's soft flash, and — when the answer killed it — the creature's FALL (its death clip, sound and plume). `HuntScreen` holds them as `ReactionEcho`s on the reaction that answered and releases them when `ReactionPerformance.Update` reports the stop; a seek that rewinds past the bite drops them. A creature the answer killed is drawn STANDING in the jaws until then (`_deathDeferred`), then falls, and the jaws hold 50 ms and let go. The enemy's bite keeps its own thud, burst and number at t 0 |
| Target the actual creature | the reflected `Strike` events after JAWS' `Skill` event at the same ms (`ActionTargets.StruckBy`); the clamp point is read ONCE off the creature's own silhouette (`SilhouetteProbe.FrontLower`: its front-most opaque pixel in the lower body, facing the champion, inset into it) and kept as a share of its body, so the jaws ride its bob, lunge and recoil |
| The chain belongs to the Seeker | its end is his BELT (`ChampionAnchor`, a share of his DRAWN body: it rides a HARD HANDS leap); a DARK METAL body (`prop_seeker_chain_body`, a cross-section stretched along 14 segments of the curve, no gaps) with 11 link ACCENTS, dense at the belt and at the trap's eye, sparse across the middle (the first build stamped up to 64 equal links); it hooks on at the eye on the base's rear end |
| Material vs Source | iron untinted (AlphaBlend); the Source is light only: the tether's streak as it fires (60 ms) and one brief TOOTH-EDGE glint at the stop (the far jaw's teeth mask, and the near jaw's only when it is not behind the creature; peak 0.6, 60 ms: at 0.9 the teeth flashed white, a row of white points that leant toward a grin). NO sparks: pin sparks were reviewed at play size in the identity pass and were 3-4 px specks nobody sees (`Sparks` = 0; the code throws them from the pins if a recipe asks). The silhouette survives with the Source off. No generic flash, no fan |
| Death is the stronger state | a caught creature the answer kills: the jaws shut on it standing, it falls on the stop, the jaws hold 50 ms, let go and are reeled home early; they never pull a corpse. A champion felled by the same bite: JAWS still answers; after the stop his end of the chain goes down, so it slackens and the trap fades where it is (no retract through his fall). The Downed state runs the playhead on for the reaction alone |
| One authored sentence | the enemy's bite thud stays (the cause, t 0); ONE cue `sfx_seeker_jaws_snap` played on the first frame, a MECHANISM, never a bite: a tiny latch tick and chain rasp at 0 ms (−23 dB under the peak), then its strongest transient, the DRY FORGED-STEEL CLACK, at 51 ms, on the frame the jaws hit their stops (a hard broadband contact and a few inharmonic partials that die inside ~12 ms, the second jaw's stop 3.5 ms behind, a muted leather contact ~20 dB under it), then three small chain-link ticks. The ear hears BITE → a tiny release → CLACK; the gap helps the eye find the close. The identity pass's material is kept as the baseline (87 % of the energy above 1.2 kHz, no bone); only the clack moved, from 17 ms to the new stop. The cast breath, the reaction thud, the answer's generic thud and puff, the row ring and the "SNARE" callout are gone for JAWS; the target flash is soft (0.18, 90 ms) |
| Secondary under an action | the snap is not `lead`, so an action's duck plays it at 0.45; its light plays at 0.55 while an action is in its focus (`IReactionStage.ActionInFocus`) |
| The dock owns ARMED / REARMING | the tile sweeps from the trigger to the moment the fight REPORTED it armed (`ReactionArmed`, below), dims while rearming, and crosses to ready with a RESTRAINED pulse: a thin gold line leaving the rim (to 1.14×) and a 6 % swell, where an Active opens its wide ring at 1.6× (compared at true speed; `RH_REACTION_READY=ring` restores the wide one); a 0 ms window never shows ready (no frame sees it armed); no sound |
| Actions ignore the yank | `IActionStage.TryTargetBody` is the creature's own place; only `IReactionStage.TryCaughtBody` sees the yank (a HARD HANDS leap planned into a pushed creature measured the push, 671 → 673 px, before this rule); proven again after the polish: every SPRAY / HARD HANDS moment identical with the layer on and off |

**The Core readiness event (D3).** `BattleEventKind.ReactionArmed`: Slot = the skill slot, Amount = how long the rearm
ran, AtMs = the moment the fight considers the reaction ready. It is read off `Champion.ReadyAt`, the table the fight
decides by, so RearmMs, CooldownMultiplier, COILED, the live action rate and a LOOSE AGAIN clear all reach it; nothing
in the Game rebuilds it. When readiness and the next trigger share a millisecond, ARMED comes first. A ready moment
after the wave's end is said by the next wave (its start is then before 0); `SoloExpedition.ReactionReadyAfterLastWave`
gives the wave's last trigger its end meanwhile. It decides nothing: nine build shapes over three waves resolve to
the identical fight with the report on and off (`reaction_armed_test`), and every report agrees with what the fight
then does (armed = the next bite is answered; rearming = none is).

**What the report found.** The discovery measured a 0 ms armed window by adding RearmMs to the last trigger, the very
reconstruction D3 forbids. The fight's own number differs: this Seeker acts at ×1.06, so JAWS rearms in 2830 ms and
waits ARMED 170 ms before each bite at normal TEMPO (417 ms at fast TEMPO, rearm 1583). A 0 ms window needs a rearm
that is a whole number of the fight's 100 ms ticks; the Core tests pose one and prove its order.

## The field reference: PRESS (ACCEPTED 2026-09-29: the GOLD-STANDARD FIELD / AURA reference, the field-propagation picture of `fab25919` and its human-approved tick cue, candidate A BALANCED)

**ACCEPTED: the gold-standard FIELD / AURA reference (the owner, 2026-09-29).** "PRESS is APPROVED. Use Candidate A -
BALANCED as the final human-approved PRESS tick cue. Do not modify the approved visual from `fab25919`." PRESS is CLOSED.
Beside SPRAY (PROJECTILE / TRAVEL), HARD HANDS (MELEE / DIRECT CONTACT) and JAWS (REACTION), it is the reference a future
PERSISTENT FIELD / AURA follows:
- **Architecture:** a skill-id recipe (`FieldRecipes.For`), its own field layer (`FieldPerformance`, built per wave from
  the fight's Aura / Break events; never a committed clip, never the champion's body), the fight's own tick as the
  moment of arrival, the real pressed target, giving way to JAWS on a shared tick; Core untouched.
- **Picture** (the visual contract of `fab25919`, unchanged): a quiet persistent field that compresses and snaps before
  each tick; one pressure front that travels LEVEL as a wall (no head, no tail, no glowing centre, never aimed), ARRIVES
  on the tick, stops, and FOLDS into the crush where it stopped; two arcs close only vertically (~30 ms in, ~60 ms hold,
  the body squashed x0.81 / x1.09, no push); the game's pixel material; quiet under an action, dimmer when it yields.
- **Sound:** ONE composite cue on the crush, never on the launch and no travel audio: `sfx_seeker_press_tick`, candidate
  A BALANCED, built from REAL recorded foley (`make_press_tick.py`, CC0 sources in
  `tools/asset-pipeline/foley/press_tick/`): PRESSURE RELEASE (air) -> COMPRESSION THUMP (the main transient, landing in
  the crush) -> a focused, brittle DEFENCE CRACK immediately after it -> a short decay, no reverb tail. `TickVolume` 0.28,
  never lead, under SPRAY / HARD HANDS / JAWS; x0.6 on a quiet tick (an action close by or the champion performing, the
  duck alone when ducked), x0.5 when it gives way to JAWS. The approved bytes are pinned by their SHA-256 in
  `press_field_test.cs`: changing the cue is a new approval, never a regeneration. B and C were not chosen and are
  review history (`tools/asset-pipeline/audio_history/press_tick/`).
- **Decided, not built:** no special ducking of the enemy's bite that lands on the same tick (two valid gameplay events;
  A's crack gives the semantic separation; revisit only if a real gameplay mix test shows one of them inaudible); no
  dynamic volume scaling at a faster tick cadence (it belongs to variation-specific testing if PRESS reaches much faster
  tick rates in production); no CRUSHING / PIN / SEIZE variation yet (the three layers are the foundation they vary).
- **The method that got here:** one visual sentence approved before any polish, each pass judged by independent lenses
  at true speed on the trace clock, the picture closed before the sound, three structurally different cues from CC0
  foley measured (placement, loudness K-weighted, tonality) in the fight's own mix, the owner choosing by ear.


**The brief** (the owner, Concept A, after JAWS closed): PRESS is the gold-standard candidate for the PERSISTENT FIELD /
AURA family. "A persistent pressure field around the Seeker periodically sends a force pulse outward. When the pulse
reaches the front enemy, that enemy is visibly crushed / compressed for a moment." One visual sentence: the Seeker's
field pulses -> a wave goes out -> that enemy gets crushed; the player reads that the field is active, that a tick
occurred and which enemy was affected. Motion reference: Syndra E (League of Legends), in the Seeker's lavender / violet
/ shadow palette with JAWS' material restraint. Not a projectile, a buff ring, an AoE blast, SPRAY or JAWS.

| Concern | Where PRESS does it |
|---|---|
| Core untouched | PRESS is a Field: each `IntervalMs` (2000) the fight emits `Aura` for its slot and a `Break` on the first creature standing. The screen builds the wave's `FieldPerformance` from those events (`BeginWave`): each tick's ms, the creature its Break names (else the first standing) and whether a presented reaction plays around it |
| Belongs to its SKILL | `FieldRecipes.For(character, SkillDef.Id)` (only `seeker` + `hammer_press`); another field keeps the generic held aura (`HoldAura` returns when a field is performed); `RH_FIELD_RECIPES=0` is the with/without switch |
| Contact on the beat | u = playhead - the tick's ms: compression -520..-300, the front leaves at -300 and ARRIVES at 0, the crush from 0, the phrase ends at +380 (it fits between ticks) |
| One sentence, five states | A a quiet haze leaning toward the enemy side, forward of the Seeker, alpha ~0.26 + a faint edge glow (drawn behind him, centred on him, it was invisible); B it draws in (0.86) and gathers (0.5); C it springs out and one crescent front (thick smoky wake, a rim brightening to arrival) leaves its leading edge and grows from 0.5x the Seeker to 1.7x the target; D the crescent's tips fold over and under the target into two arcs biting into its drawn silhouette, pale-lavender hot on the tick (the brightest moment), and its body buckles (x0.85 / x1.07, feet on the floor, `UiKit.Buckle`); E the arcs let go cooling to violet |
| Which enemy | the arcs sit on the target's DRAWN silhouette (pinned once per tick with `SilhouetteProbe`, as JAWS does), 0.8 of its width, centred 0.42 from its front; they start at most 0.66 of its height out (inside the stage) |
| Gives way to JAWS | a tick with a presented reaction triggered -700..+400 ms around it draws no arcs (with JAWS' fang rows they read as one jaw); the front flattens against the creature's face, gone by +100 ms, and the body still buckles |
| Quiet under an action | x0.6 while the champion performs (SPRAY, HARD HANDS) |
| Layers / cost | the field under the figures; the front and the arcs over the creatures and under the Seeker; their light in the shared additive pass; at most 10 sprites, alloc 0 (the once-per-tick silhouette probe aside) |

Three procedural parts (`tools/asset-pipeline/v2/seeker_press.py`): `fxp_seeker_press_field`, `_wave` (crisp + softened),
`_clamp`. Tested in `press_field_test.cs`; evidence `production/qa/evidence/press-field/` (films by
`tools/asset-pipeline/films_press.sh`); review page https://claude.ai/artifact/EL1ZzQ9EjMYhqw13aF83CG. Checked by a three-lens judge panel (ACCEPT WITH NOTES;
two must-fixes, both fixed) and two adversarial re-checks (RESOLVED WITH NOTES). Open: the buckle hides inside the pack's
lunge in this seed (every PRESS tick lands on the pack's own bite beat); no sound yet; the arcs may still read as
brackets; the owner's review. (The owner approved the direction and asked for the pixel-hard pass below.)

### The pixel-hard pass (2026-09-28, APPROVED by the owner)

**The brief** (the owner approved Concept A's direction; the craft was too smooth, too soft and too vector-like and the
tick lacked hardness): bring PRESS into the game's pixel language and make the tick substantially harder, from timing
rather than more VFX. Keep the sentence (quiet field -> contraction -> one broad front -> front-enemy crush -> settle),
the real target, the JAWS yield and the quiet mode. No rings, arrows, projectiles, sparks, debris, shake, bloom or white
target flash; PRESS stays subordinate to SPRAY, HARD HANDS and JAWS ("hard" is decisive, not larger or brighter).

| Concern | The pixel-hard PRESS |
|---|---|
| Pixel material | the front is built at 1/5 resolution and the arcs at 1/8, then upscaled NEAREST: one art pixel is ~3 screen px at the capture size, the stage's own pixel (iteration 1's arcs at ~1.5 px read as a second material); shape first, a 0.4-source-pixel soften, no feather on the leading edge |
| Drawn on the grid | the front is drawn AXIS-ALIGNED on whole screen pixels (turned along its ~12 degree path, the NEAREST grid tilted off the stage's and its rim read as a serrated saw-tooth) and leaves the field at 0.87 of its arrival height (grown from half the Seeker, its art pixel slid from ~2 to ~3 screen px in flight) |
| Value bands | DARK wake, MID violet mass, LIGHT lavender leading edge, PEAK only a few art pixels at the contact (white/grey parts tinted at runtime) |
| One broken front | five stepped segments, two notches in the front and two in the back; no noise masks (random masks read as grain) |
| Afterimage | the soft ghost is gone: one SOLID darker echo one frame (17 ms) behind at 0.25 |
| Release | tight -> snap -> settle: the field contracts to 0.82 (-520..-300), snaps to 1.10 within ~27 ms and settles by +110; the front accelerates into contact (travel eased ^1.8; its rim 0.3 on the way, whole on arrival, a lavender line at 0.6 of the front's opacity) |
| Crush | closes in 30 ms (was ~70), holds 60, lets go in 120; the body buckles to x0.81 height / x1.09 width, feet planted; the phrase ends at +300 (was +380) |
| The heat is a line | the arcs' body stays violet on the tick; the heat lights only their pressing EDGE (a fifth clamp cell: a dim line and a four-art-pixel PEAK at the contact). Lit whole (iteration 2), the arcs made the tick as bright as SPRAY's hit and twice HARD HANDS' |
| Which enemy | the target's drawn body is pinned at launch for the travel and pinned again one frame into the crush, so a creature caught in its own lunge is pressed where it is drawn (on the launch shape the arcs sat behind its head and on the next creature; followed every frame, they jumped with each lunge frame). The crush pose only PLACES the arcs: centred 0.33 from its front (head and shoulders), on the body as it BUCKLES, so they press it down and touch it (on the upright body they hovered 35-40 px off it); their SIZE comes from the launch silhouette (0.85 of its width: their art pixel is the world's; sized from a lunge's long silhouette they grew coarser) |
| The arcs' shape | bowed toward the body and thinner (a shallow thick slab read as two bars or platforms) |
| Settle | a pixel dissolve: the arrived front and the released arcs erode in 3x3 holes, fragments falling 1 px; no emitter |
| Dropped | iteration 1's "> TARGET <" chevrons (all three judges: a lock-on reticle / arrowheads, landing on the next creature, pinching sideways against a top-down crush) and the 6 px micro push (not visible at true speed; unanimous) |
| Quiet | also when an action's skill event lands -200..+350 ms around the tick (the champion may be idle again by then) |
| Yield | the front that gives way to JAWS flattens in its own violet (the dissolve cell unsqueezed: a squeeze smoothed its pixels) and only its leading edge stays lit (the hot tint over the whole cell was a pale slab beside the bite) |

Parts regenerated by `seeker_press.py` (front: body, edge, echo, dissolve; arcs: whole, three erosion steps, edge); the
smooth parts and the chevron sheet kept in `tools/asset-pipeline/v2/keypose_sources/history/`. Evidence
`production/qa/evidence/press-hard/` (`tools/asset-pipeline/press_hard_evidence.py`); review page https://claude.ai/artifact/YZNuW29wsUsxXkzJMpN88i.
Checked across four iterations: a four-lens panel on iteration 2 (three ACCEPT WITH NOTES, one REJECT: the whole arcs
lit pale, the turned front, its sliding pixel size, the arcs off the lunging target), three re-checks on iteration 3
(all resolved; a new must-fix: the arcs sized from the lunge pose, hovering off the buckling body) and two final checks
on the pass (ACCEPT WITH NOTES, no must-fix). The owner's seven questions, answered on the page: native pixel art YES,
one front YES, substantially harder YES, the affected enemy clearer YES, less smoothing helped YES, quieter than SPRAY /
HARD HANDS / JAWS YES at every peak (summed over the phrase the travelling front is lit longer than HARD HANDS' two-frame
hit), the push NO. Open: no cap on the arcs' size for a much larger creature; the bottom arc rests on the floor under a
lunging creature; the arcs placed once float over a creature whose lunge ends mid-hold; the buckle on a standing creature
is unseen in this seed; the front leaves at nearly full size; the field haze stays smooth; no sound yet (the pass stops).

### The field-vs-projectile polish (2026-09-28, APPROVED by the owner: the accepted FIELD / AURA visual direction, commit `fab25919`)

**The brief** (the owner): the pixel-hard PRESS is APPROVED; do not redesign it (keep the material, the palette, the
persistent field, the ~30 ms crush, the ~60 ms hold, the squash, no push, the brightness hierarchy, the yield rules). One
question remained: does the travelling front read too much like a PROJECTILE? PRESS must read as a FIELD DISTURBANCE
PROPAGATING THROUGH SPACE, not a magic projectile fired at a target. No added weight.

| Concern | The field-propagation PRESS |
|---|---|
| Never aimed | the front travels LEVEL along the combat axis, halfway between the field's height and the target's (`WaveAxisShare` 0.5), clamped so the arriving wall always holds its target: it never climbs or dips toward the target (the pixel-hard front came down ~12-16 degrees from the Seeker's chest, homing); first tried at the target's own height, it was born under the Seeker's belt, below the field |
| A wall, not a body | no pale accent at the middle of its edge (a glowing centre is a head) and no one-frame echo behind it (a trailing copy is a tail): the front is only the wall |
| The field discharging | launch contours were tried (two thin broken lines of the front's curve ringing at the field's boundary as the front departs, ~70 ms) and REMOVED: three of four judges measured them invisible at play speed and, on a still, speed dashes behind a moving body -- the trail the brief rules out. The link to the field is its contraction and snap, and the front leaving its edge at its height |
| The front becomes the crush | on contact the front stops; the first frame drawn after it shows the FOLD, exactly once (a new part, `fxp_seeker_press_fold`: its wall, keeping the front's mass, collapsing at the creature's middle and curving round into ends bent over and under it), in the front's violet with its edge lit at 0.72 of the arrival rim's light (at the full light its longer edge made it the brightest frame; at 0.55 the darkest), over the arcs' OWN span around the creature's upright middle; the arcs take over the frame after. The light pass knows the fold frame by a frame count (by the playhead, a frozen end-of-wave clock redrew its lit edge every frame) |
| Forward motion dies | the crush forms WHERE THE FRONT STOPPED (`FoldBackShare` 0.35 of its width behind the stop, the rest over the creature), latched on the first frame after the contact whatever the frame phase, and the arcs only close VERTICALLY (the upper presses down, the lower up); the pixel-hard arcs slid on from the arrival point and drifted with the creature through the hold. Placed from the creature's silhouette, an early first frame used its launch pose and the next its pressed one: a forward lurch, then a snap back (and a double fold) on most 60 fps phases -- the capture's on-tick takes hid it, the fast take showed it |
| Unchanged | the material, the palette, the field, the crush (30 ms in, 60 hold, x0.81 / x1.09), no push, the brightness (peak 746 px above luma 170, as the approved version), the quiet and yield rules |

Parts regenerated by `seeker_press.py` (front: body, edge, dissolve; fold: body, edge); the approved pixel-hard front
kept in `tools/asset-pipeline/v2/keypose_sources/history/` (`fxp_seeker_press_wave_pixelhard.png`). Evidence
`production/qa/evidence/press-propagation/` (`tools/asset-pipeline/press_propagation_evidence.py`); review page https://claude.ai/artifact/2zZPnjMVQqSTtunKw8RxQw.
Checked over three iterations: four lenses on the first (two REJECT: the fold's reach and snap back, the dim fold, the
arcs' drift, the front born below the field; the contours: remove), four re-checks on the second (all resolved but a
phase-dependent lurch, which all four rejected) and two final checks on the pass (ACCEPT WITH NOTES, no must-fix; the
phase fix resolved at every contact; a frozen-playhead fault they found is fixed). The owner's four questions, answered on
the page: still a projectile NO, mostly (the approved travel -- one crescent speeding into the hit -- is what remains);
propagation from the field PARTLY (released by it more than rippling through it); the horizontal -> vertical transition
YES; as readable as the approved version YES (the lunging creature's snout 60-80 px ahead of the arcs for ~50 ms is the
cost). Open: that trade (`FoldBackShare`), the lower arc's weaker press (feet planted), the approved speed-up, the fold's
one-frame bracket-like flip on a still; no sound yet (the pass stops).

### The final audio (2026-09-29, candidate A BALANCED HUMAN-APPROVED; B and C are history)

**The brief** (the owner): the PRESS picture is APPROVED (commit `fab25919`, the field-propagation version) and is not
reopened. Complete PRESS with its final audio. Its meaning is PRESSURE ARRIVES -> TARGET COMPRESSED -> DEFENCE BREAKS; it
must not sound like a projectile impact, a sword strike, an explosion, JAWS' bite or HARD HANDS' heavy melee hit. ONE
composite cue of three layers, on the crush and never on the launch, no travel audio, under SPRAY / HARD HANDS / JAWS, not
fatiguing on repeated ticks. Up to three candidates; the owner approves one by ear. No future variation is implemented.

| Concern | The PRESS tick cue |
|---|---|
| One event, three layers | `sfx_seeker_press_tick` (~190 ms file, ~120-135 ms audible): a PRESSURE RELEASE (0-35 ms, noise-like air, not a tone: the air a canvas snap, a pillow and a paper stack push out, and a small synthesised sub), the compression THUMP at 40 ms (the strongest layer: padded cushion hits, a book slammed flat, a flour sack), a brittle, material-neutral defence CRACK +6..+13 ms after it (a ceramic tile, a walnut shell with its 11 kHz ring notched, a stone break, an ice-tray tick), then a very short fragment decay and no reverb. Built by `tools/asset-pipeline/make_press_tick.py` from recorded CC0 foley (the licence checked on each sound's own page; `tools/asset-pipeline/foley/press_tick/SOURCES.md`); two buses (low, crack), light 4x-oversampled saturation, band 28 Hz-15 kHz, the loudest 50 ms at -18.5 dBFS, true peak about -9.6 dBFS; byte-reproducible |
| On the crush, never the launch | `FieldRecipe.TickCues`, asked once per tick by `FieldPerformance.CueDue` (a rewind re-arms it; a seek more than `CueLateMs` 30 past it skips it), started `CueStartMs` -20 before the tick so the file's thump (`CueThumpMs` 40) lands IN the crush: at the game's 60 Hz step the ask fires at -16.7 ms, the thump at +23.3 (between the fold frame, +16.7, and the arcs shut, +33.3) and A's crack at +31.3 (on the shut frame), on every tick on the trace clock. Nothing sounds as the front leaves the field or travels |
| Under the other three | `TickVolume` 0.28 and never lead: an authored action's duck applies. K-weighted loudest 50 ms as played, A -31.3 dB: 3.8 / 7.8 / 5.2 dB under SPRAY's contact (-27.5) / HARD HANDS' impact (-23.5) / JAWS' bite (-26.0). x0.6 (`CueQuietShare`) on a quiet tick, the picture's own rule: an action's contact close by, or the champion performing one; a quiet tick already under the duck takes the duck alone (the product buried the crack under SPRAY); x0.5 (`CueYieldShare`) on a tick that gives way to JAWS |
| Repeated ticks | the game's small pitch variation (+-0.03 octave, `CueVary`) and a pan from the pressed creature (`CuePanWidth` 0.35); the crack's body and tail kept short, its initial transient kept |
| The wave-end tick | a tick that ends the wave now plays its crush out on the break's clock (`FieldPerformance.Crushing` holds `ActionStillPlaying`); it froze on its arrival pose while its cue sounded |

| Candidate | pressure (vs the loudest 50 ms) | crack after the thump | crack (vs the loudest 50 ms) | character |
|---|---|---|---|---|
| **A balanced** (HUMAN-APPROVED, the game's cue) | -14.3 dB | +8.0 ms | -7.2 dB | pressure, thump and tile crack in proportion; the thump leads |
| B heavier pressure | -11.0 dB | +13.0 ms | -6.9 dB | an earlier, heavier air push and a squeezed sack; the crack later, a shell giving way |
| C clearer defence break | -13.7 dB | +5.9 ms | -4.9 dB | the clearest break: a brittle tick on a stone fracture and a tile |

**The owner chose A (2026-09-29), the recommendation.** All five QA lenses over two rounds chose it: its pressure is the least tonal (air, not an
808), its thump leads the crack by the widest margin, and its crack is the most focused (most of the crack window at
2-6 kHz). That crack is what tells PRESS apart from the pack's bite, which lands on every tick in this seed (enemies
strike every 1000 ms, the Aura ticks every 2000) and fuses with the thump; A's thump keeps the best margin over it
(+10.7 / +6.2 / +4.6 dB on a full / quiet / yield tick). B's pressure keeps a faint ~86 Hz periodicity; C's crack comes
close to taking the lead from its thump.

The review films are timed on the trace clock: `film_audio.py` lays every captured frame for its real duration (an ffmpeg
concat listing with a millisecond timebase; the default 1/25 s rounded the durations and dropped half the frames, the
fold and shut among them), resampled to 60 fps; before, a constant rate drifted the picture up to ~60 ms against the sound.
Evidence `production/qa/evidence/press-audio/` (`tools/asset-pipeline/press_audio_evidence.py`); review page https://claude.ai/artifact/13QvWjjw4rQvxSMgyjh2c1.
Checked by a first QA round (design, mix, sync: all A; it found the cue 1.6 dB under SPRAY, the wave-end freeze, the
film's drift, quiet x duck, a late window of 80 ms, B's tone, C's crumble, the walnut's 11 kHz ring, all fixed) and a
re-check (design-mix, sync: A; it found the film's 1/25 s timebase and the cue ignoring the picture's quiet rule while
the champion performs, both fixed). The owner's decisions on what stayed open (2026-09-29): the pack's bite fused with
the thump is NOT ducked (two valid events; A's crack separates them; revisit only if a real gameplay mix test shows one
inaudible); no dynamic volume at an upgraded 0.7 s cadence (variation-specific testing, if production reaches it). Noted:
a yield tick under a duck stacks the two (x0.063, a deliberate give-way); device and display latency are not modelled.
The CRUSHING / PIN / SEIZE variations are NOT built; the three layers are the semantic foundation they would vary, keyed
by skill id like the picture. **HUMAN-APPROVED (the owner, 2026-09-29):** candidate A is the canonical
`sfx_seeker_press_tick` (SHA-256 `bd3c390944f07458daef278e165c15046b999274a7b2ad86164ce6d62e8fd451`, pinned in
`press_field_test.cs`); `make_press_tick.py` writes it to the game and B and C to
`tools/asset-pipeline/audio_history/press_tick/`; PRESS is ACCEPTED as the FIELD / AURA gold-standard reference (above)
and CLOSED; the visual contract of `fab25919` is kept.

## The mark reference: BRAND (FIRST SLICE 2026-09-29, awaiting the owner's review: the Etched Shadow Brand, the MARK / PERSISTENT TARGET-ATTACHED STATE candidate)

> **STATUS 2026-10-02: BRAND is ACCEPTED and CLOSED as the GOLD STANDARD for MARK / PERSISTENT TARGET AFFLICTION.**
>
> - **Visual: APPROVED.** Living Shadow Corruption: separate infected territories, the host's own material corrupted, Shadow-violet with host-adaptive Ash-Burn, depth = more of the body corrupted (from `39b59aaa`; the etched mark / scar / rune / vein-network families below are history).
> - **Renderer: APPROVED** (ADR-013): a production shader pass over baked host metadata, no combat GPU read-back, bounded draw / batch cost (2 / 2 per cursed body), zero steady and event managed allocation (evidence `production/qa/evidence/brand-production/`).
> - **Audio: APPROVED, Candidate A / SUBTLE / INTERNAL** (the owner, 2026-10-02: "Candidate A is approved as the canonical BRAND sound family."): seven `sfx_seeker_brand_*` cues (apply, deepen, deepen_deep, infect, leave, awaken, Ash accent) from CC0 foley, scheduled by `MarkVoice` on the curse's own presentation timeline; no idle loop; subordinate to SPRAY / HARD HANDS / JAWS / PRESS; the bytes are pinned by SHA-256 in `brand_audio_test.cs` and traced to their sources and generator by `tools/asset-pipeline/foley/brand_curse/brand_audio_manifest.json`; B (SUPERNATURAL) and C (ORGANIC / ASH) are rejected and archive-only (`tools/asset-pipeline/audio_history/brand_curse/`). Brief: `design/audio/seeker-brand-audio-brief.md`; evidence `production/qa/evidence/brand-audio/`.
>
> **What the gold standard means.** Future persistent target-affliction effects take BRAND as the reference for its PRINCIPLES and quality bar, not its art: change the victim rather than place a UI-like symbol on it; quiet persistent presentation, stronger presentation on state change; depth expressed structurally; host-adaptive contrast; readable propagation and transfer; coexistence with action / reaction / field VFX; production-safe rendering; restrained event-driven audio rather than persistent sound. A future mark need not use purple corruption territories.

**Status: a first slice, NOT accepted.** Built, filmed and reviewed by independent lenses over six rounds; the
owner has not seen it. Beside SPRAY (PROJECTILE / TRAVEL), HARD HANDS (MELEE / DIRECT CONTACT), JAWS (REACTION) and PRESS
(FIELD / AURA), BRAND is the candidate reference for the last archetype: a STATE that lives ON an enemy. Evidence
`production/qa/evidence/brand-mark/` (discovery `DISCOVERY.md`); review page https://claude.ai/artifact/AddEshDzufkCDuBmyJitpL.

**The brief** (the owner): "That enemy has been branded. The mark is living shadow, etched into its body. It quietly
persists there. When the mark deepens, it bites further inward. If it spreads or transfers, the same mark migrates
cleanly to the next target." A hybrid: an etched / branded sigil made of shadow smoke / ink. Not a projectile, a field,
a trap or a champion performance; no UI icon, reticle, rune disc, orb, clean logo, fog blob or sticker; no teeth (JAWS'
language), no steel or shards (SPRAY's, HARD HANDS'); quieter than every accepted reference. Discovery first; PixelLab
for sources, deterministic assembly preferred; Core untouched but for a proven information-only hook.

**What BRAND really is** (Core read, then re-derived by running it; `DISCOVERY.md` §1): a Field (`sign_brand`, Amplify,
2000 ms). Its host is the FRONT enemy (`FirstAlive`, re-evaluated on every hit); SPRAWL marks every creature at half
strength (ANCHOR keeps the front at full). The mark starts on the wave's first tick (2000 ms; 1000 with PACE) and lasts
until the wave ends. Base BRAND never deepens (+70 %); ETCH deepens on every tick (120 / 170 / 220 / 240 %), and the depth
belongs to the WAVE, never reset by a death. There is no transfer event (the next creature is the front at once) and no
consume.

| Concern | Where BRAND does it |
|---|---|
| Core: one information-only hook | the field tick's `Marked` is emitted AFTER the depth update, so it reports the depth IN FORCE (it said 0 % on every wave's first tick and ETCH one step late). Proof: `brand_marked_report_test` pins a fingerprint of every other event, every wave outcome and the champion's end state for ten builds, taken on the code BEFORE the move: byte-identical after it; and the reported depth is what the front pays |
| Belongs to its SKILL | `MarkRecipes.For(character, SkillDef.Id)` (only `seeker` + `sign_brand`); CALL and OATHMARK share the art key "mark" and are untouched; `RH_MARK_RECIPES=0` is the with / without switch |
| Fields chosen by recipe | `FieldRoles.Choose`: the performed field (PRESS), the mark (BRAND) and the held aura are picked by recipe, not slot order. Before, the FIRST field won: BRAND woven before PRESS erased PRESS's accepted picture |
| A layer, built from the fight | `MarkPerformance`, a pure function of the playhead, built per wave in `BeginWave` from BRAND's Aura ticks, the `Marked` depth at the same ms, and the falls the SCREEN shows (a JAWS kill falls on the snap); a host chain retargeted to the creature standing when the smoke lands |
| On the body, authored | drawn in the creature loop after the creature and before its hit flash (a creature in front covers it; a hit whitens body and brand together), at an AUTHORED body point per strip frame (`<strip>.mark.json` for all 60 creature idle / attack strips and all 30 death strips (a falling host is pinned from its death clip's point), `MarkPoints`, mapped with the draw's own transform so it rides the breath, the lunge and PRESS's buckle). The points are proposed by `mark_points.py` (a reviewed seed per strip, tracked frame to frame, the coil kept on the body; a few strips authored by hand, two holding ONE spot that follows its own patch of the body; a shield counts as the body) and reviewed on contact sheets; each carries a HEAD box per frame and the mark's whole drawn extent (`MarkRecipe.HaloWidthShare` 0.42 x `HaloHeightShare` 0.35 of the body height, measured on the atlas by a test) is kept clear of it: on the torso, the mark's whole smoky halo clear of a reviewed HEAD box per frame, never between two creatures' faces; one point for two identical frames; no jump over 0.16 of the frame, the loop's wrap included. A luma "eye" rule was tried and removed (it found the head, legs, staffs and spike tips on other families). `SilhouetteProbe.Torso` is only the fallback for a strip with no file |
| Size | 0.28 of the canonical body height (a whelp's coil ~60 px), snapped to thirds; depth never changes the box |
| Material | one atlas from `seeker_brand.py`: PixelLab's hand-burned coil silhouette (pixen 1bf52462) redrawn at the game's pixel material (3 px a logical pixel, value bands, nearest, no fragment under 6 texels): a ring, its open hollow and a thin inner hook. A HALO of smoky roots drawn twice (near-black ink that reads on a light body, lit lavender-grey smoke that reads on a dark one), never filling the hollow, then the CUT: a translucent groove whose lit core crawls along a third of the ring. No white anywhere |
| APPLY (the wave's first tick) | smoke clumps gather on the body (-230 ms) and condense into the coil; the whole cut lights once ON the tick (0.8, the moment Core amplifies) and cools over 220 ms; no burst, no ring |
| IDLE | the coil; the lit third of the ring steps every 800 ms, each creature at its own moment; the cut and the smoke roots never change shape; the hook is never lit; no pulse, no ring, no cloud |
| REFRESH | a tick that changes nothing (base BRAND) shows nothing |
| DEEPEN (ETCH, WINNOW) | ONE composition at every depth, CUT FURTHER INWARD, never bigger: the groove widens (deep1), the ring's wall is cut into the hollow (deep2), then the thin hook is cut further ALONG THE SPIRAL, inward (deep3, deep4: the path runs on past the source's inner end at its own curvature; deep1 to deep4 share one outline box). The spiral stays OPEN at every depth (fused, it was a ring round a pupil). Rejected on the way: a thickened, curled or lit inner end (an S / 5 / 9 / @ or a pupil), a hollow shrunk round the hook (an eye), a burn spreading out of the ring ("just bigger"), a bevel (flat). Design D was chosen unanimously by three blind readers over four ladders ("etched further inward at every step"). The old cut holds while a bite near the tick plays out (`SettledStart`: no chisel travel over a strike's wind-up (150 ms) or its clip (300 ms)), then a chisel lights ONLY the new cut in three stretches (round the ring; on the hook, setting in at the old cut's end and running on inward), and the new stage appears under it |
| One shown stage | a coil shows the stage it arrived with and moves deeper only when a chisel's last step reaches it: a tick's, ANCHOR's at a fall, or a CATCH-UP chisel once a migrated coil is whole again. A deepen its host fell in the middle of is carried to the next host and cut there, never lost; the loosen shows what the host showed |
| MIGRATE (the host falls) | the coil comes apart into smoke on the falling body (220 ms); the next creature, amplified at once, carries a thickening smoke; a tapered strand of overlapping puffs slides under the heads to its coil's tip and the coil is drawn in from there (~300 ms) |
| SPREAD (SPRAWL) | every creature carries it from the first tick (a faint smoke until its hop lands); hops leave as the previous lands (the row forms in ~0.6 s), never from or to a fallen creature; a WINNOW deepen ripples down the row (1.5 chisel steps a place), never a whole-row flash |
| CONSUME | none: BRAND has none |
| Quiet | a beat is drawn at half strength when an action's contact is -200..+350 ms from it, a presented reaction -700..+400, or PRESS's crush on the same ms |
| Cost | 3-6 sprites a host, one atlas, no Effect, 0 bytes a frame (the body point is data; no strip is read for a creature that has one) |

Measured (18_measures.md, exact with / without pairs): the apply adds 765 px above luma 170 (0 above 210), rest 0, a clean deepen 135 / 63, SPRAWL + WINNOW's ripple 347, against PRESS 749-867, HARD HANDS 1382, SPRAY 2761, JAWS 6957; each ETCH step in game deep1 1170 -> deep2 +19 % -> deep3 +6 % -> deep4 +6 % violet lit px; 0 bytes on every drawn frame of all 16 on-mark takes.

Tested in `brand_mark_test.cs` and `brand_marked_report_test.cs`. Open: no BRAND sound yet (a later audio pass, as
PRESS's was); ETCH's card says "to +170 %" while Core reaches +240 %; the inspector's "YOUR SKILLS" / any-creature
wording; CALL + BRAND and SPEND + BRAND interplay; LEGION children beyond the creature row; the white fx_death smoke of a
fall covers the next host for ~0.3 s (another effect); a JAWS-kill fall is unit-tested, not filmed; the owner's two decisions -- the deepest ETCH steps are small (+6 % each: accept, show three depths, or a new shape pass) and the material reads as lit line more than incised shadow (recommended: an incision behind its own blind read); for ~100 ms at a fall the loosening smoke passes close to the next whelp's eye; a short hop's strand is a vertical wisp.

### The second pass: the Etched Shadow Cut (2026-09-30, awaiting the owner's review)

The owner kept the first slice's architecture and asked for a new ART DIRECTION: the mark read "too much like a glowing
lavender spiral / eye / rune" and must read as "a dark Shadow brand CUT INTO the target". Built on
`tools/asset-pipeline/v2/seeker_brand_cut.py` (the first slice's PixelLab coil path, redrawn; the first slice's atlas kept
in `keypose_sources/seeker_brand_history/`); evidence `production/qa/evidence/brand-cut/` (the first slice's stays in
`brand-mark/`); review page https://claude.ai/artifact/A4G3gEs7CpXXoGahBEjtZm.

| Concern | The second pass |
|---|---|
| Three visible depths | `Stages` 4 (the spread cut, DEPTH 1-3), `StageFloors` {50, 150, 200}: base BRAND and ETCH 120 % DEPTH 1, 170 % DEPTH 2, 220 % and above DEPTH 3 (a rise inside a depth, 220 -> 240 %, gets only the faint retrace). The gameplay values are untouched. |
| Shape | a BROKEN ASYMMETRIC SCAR-SPIRAL, design B of three chosen by a blind read (three readers, unanimous), revised over four review rounds: three wide breaks in the outer contour at every depth (it wraps at most 290 degrees), weight on one side, scar branches added with depth, the spiral open. |
| Material, dark first | the incision near-black, on the hollow side of the stroke (open to it); a dark-violet WALL on the outer side only, two texels deep (DEPTH 1's a step lighter); ONE lit RIM on the light-facing outer arc (9-14 % of the mark); a dark ink stain, the lit smoke faint (0.18). On a light body the incision carries the mark, on a dark body the wall and rim do. |
| Never an eye | no dark texel -- core, or stain on a light host -- is ringed by the visible bands closed across any mouth under two texels (a pocket notched open by one texel still read as a pupil): any such pocket is filled with wall; tested on every depth's rest, beat and seep cells (`RingedDark`). |
| Apply / idle / deepen | ink gathers, the cut appears, its light-facing edge catches, settled dark within ~200 ms; idle holds the same cut and stain (one rim shimmer in six steps); the deepen phrase: ink gathers where the new cut will be, the new cut, its rim answers, settled by ~170 ms after the host's own bite, the growth in the visible wall band. |
| Transfer | no bridge (`TransferThread` false): the old mark collapses into its own dark centre (its outer edge violet, so it shows on a black host) while the death presents; the new front carries a dark stain and the same mark seeps in once its own bite AND the reaction it draws are over (`TransferSettleMs` 520). |
| Spread | short dark ink threads leave the front's mark for every other creature, 40 ms apart (`SpreadStaggerMs`). |
| Measures | the apply adds 108 px above luma 170 (first slice 765), the rest 0, a deepen 63-72, nothing above 210 anywhere; visible violet on the black whelp D1 -> D2 -> D3 about +46 % and +46 %; 0 bytes on every drawn frame. |

Reviewed by independent read-only lenses over four rounds (the last two: ACCEPT WITH NOTES). Open for the owner: on a
black host the dark incision cannot show (the mark there is its violet wall and rim, a scratch in the body's own hue);
the coil's size on large hosts (the Nature bruiser at scale 2.33) is the first slice's; no boss fight can be posed by the
capture rig (a still of the six bosses stands in); no BRAND sound yet (after visual acceptance).

### The final visual correction: a torn DEPTH 3 and a capped size (2026-10-01, awaiting the owner's review)

From 97efe379 the owner named exactly two faults and asked for nothing else to move. Evidence
`production/qa/evidence/brand-fix/`; review page https://claude.ai/artifact/7MGguQQXQfwXE4vRGx5PkS.

| Fault | The correction |
|---|---|
| DEPTH 3 read as a letter / number / rune / eye | UNPRIMED reads ("what does this mark look like?", nothing suggested) named the 97efe379 curl "a spiral", "@", "6", "G", "a target". Cutting its INSIDE did not help (two further rounds: "broken spiral", "@", "6", "snail shell", "cracked rune", "eye socket"): pieces laid along one curl are completed by the eye. DEPTH 3 is now TORN (`seeker_brand_cut.py`, DESIGN B, depth 3 only): the curl's four pieces, with new breaks, tapered torn ends and a wandering width, are each SHIFTED on their own (`torn`), so they no longer continue one curve; the branch that met a gash's end in a 'Y' is healed (`healed`); no scrap of incision under six texels (`least_core`: a lone black texel read as a pupil). Four fresh readers then said "bruise / stain", "torn smear", "splatter", "irregular damage" and named no eye, face or spiral; a confirmation read on the filmed game frames is in the evidence. DEPTH 1 and DEPTH 2 are byte-identical to 97efe379. The deepen into DEPTH 3 is the tear: its beat covers the moved pieces (about two thirds of the picture, dark ink; its lit rim 90 px against DEPTH 2's 63). |
| The mark too large on large hosts | MEASURED, not guessed: the front creature's visible body from the game's geometry dump on all 24 family cells and the six bosses (new capture dial `RH_SHOT_BOSS=<art key>`, presentation only), the mark's extent from the atlas (`tools/asset-pipeline/brand_scale_table.py`). Hosts grow tall faster than their torsos grow wide: at the old cap (3) the casters' mark was 49-61 % of their width at 1.67 and bruisers and bosses got 168 x 147 px at 2.33. 4/3 still left casters at 40-49 %. `MarkRecipe.MaxScale` is now 1 (the whelp's own scale): bruisers 13 % of their height, bosses 12 %, casters 30-37 % of their (staff-wide) visible width; the whelp, the pale Choir wisp and every swarm unchanged. No per-enemy case, no new mechanism. |

### The change of direction: BRAND as a CURSE (2026-10-01, three concepts awaiting the owner's choice)

The owner ended the etched-mark family (the torn cut of 5426936a is technically solid, artistically the wrong
abstraction): BRAND is presented as a persistent CURSE / SHADOW AFFLICTION the enemy suffers across its body, not a sign
on one point. Three concepts were prototyped behind `RH_BRAND_CONCEPT=A|B|C` (`CursePrototype`: the same
`MarkPerformance` truth, drawn clipped to each afflicted silhouette through the stencil buffer): A LIVING SHADOW
CORRUPTION (veins spreading from the seat), B WITHERING CURSE (the body drained to ash, blight), C SHADOW POSSESSION (a
mass inside, a double that does not fit). Unprimed reads: A "cursed / tainted" but "net / web / sigil"; B "drained /
fading / ghostly", not Shadow; C "cursed / shadow blight / darkness seeping", no symbol, faint on black hosts and near an
aura. Recommended: C, with the double kept inside the body at rest and A's legible depth. Evidence
`production/qa/evidence/brand-curse/`; page https://claude.ai/artifact/P7ZBWqXwpiihaMX9cDqYkN. The etched cut stays the default draw until a direction is chosen.

### The chosen direction: Concept A, Living Shadow Corruption, and its glow pass (2026-10-01, awaiting the owner)

The owner chose Concept A and asked for readability on dark enemies without a neon net. The corruption now grows along
PATHS from a smoky stain (not a radial texture), runs along the body's long axis, forks forward, dies early, breaks and
leaves infected patches, and never closes a loop (tested); its light is a separate additive violet emission clipped to
the body, on some stretches only, with a hot front travelling through NEW growth on apply / deepen / arrival, a SPRAWL
source that lights before its victims, and a transfer that brightens and collapses along its paths. Depth is reach.
Fresh reads were run five times and the structure revised after "a cross / a dagger" and "a belt / a sash" reads.
Evidence `production/qa/evidence/brand-glow/`; page https://claude.ai/artifact/DMw4pyqkKtUXQPBo6DvYdJ. Still the prototype pipeline (dev-only, several batches
per cursed creature); no sound; not accepted.

### Concept A polish: no beads, no garment, depth 1 on every host (2026-10-01, awaiting the owner's visual acceptance)

From d2885245 the owner named three problems. The emission is now short tapered runs inside the dark veins (no dots,
no round nodes), the steady light screened into the host and the dark body multiplied into it (a stain in the host's
material, a violet-grey cast over the whole silhouette); the structure is an off-centre entry pocket and three
territories ~120 degrees apart joined by one broken path and by smoke (a region, 1.3 : 1, never a band a garment
could be read in); depth 1 always holds the pocket's hot core and two lit veinlets in a wider pocket SEATED IN THE
BODY'S MASS (`ThickNear`: off a thin limb, a wrist, a hand or a weapon into silhouette at least 60 % as thick as its
thickest; where it landed on a wrist or beside a hand it read as the creature's own magic or jewellery), with a
HIGH-CONTRAST REGION SUITED TO THE HOST by its brightness (a dark host's tissue lit from inside by a screened violet
haze, a pale host's bruise a deep saturated violet), never by a name. Fresh-read rounds on every intermediate build,
one of them every host at depth 1, whole, at 100 %; the bead read and the seam read are gone; the generic costume
guess on robed and armoured bosses is reduced, not gone (reported). Evidence
`production/qa/evidence/brand-polish/`; page https://claude.ai/artifact/AEfLGCYB5anLk7WujnvK5n. Still the prototype pipeline; no sound; not accepted.

### Concept A territory pass: regions of the body becoming corrupted (2026-10-01, awaiting the owner's visual acceptance)

From 9aa407fd the owner named two remaining problems (long paths along clothing read as a sash / stole; on dark casters
the curse read as their own aura) and their root: visible connected veins as the persistent state. The curse is now
infected TERRITORIES (`TerritoryAtlas`), depth the number of them (1 / 2 / 3-4), seated per host from its own
silhouette and locked per creature; in each the HOST'S OWN MATERIAL changes (a drained copy faded in through the
territory's mask: colour gone, brightness pushed away from the host's own, folds kept; a stock `DualTextureEffect`),
with a restrained violet in fissures, short fragments and a broken edge, and wisps leaking out. Five probe read rounds
with stills and true-speed clips steered it (a violet patch read as costume; drained material read as damage; violet
crackle on a dark caster read as its own magic). Evidence `production/qa/evidence/brand-regions/`; page https://claude.ai/artifact/S68LvpL6kc5ah53pqmrGkG. Still
the prototype pipeline; no sound; not accepted.

## Context

The audit (`production/qa/evidence/action-presentation-audit/`) measured the Seeker's SPRAY frame by frame:

- **Target feedback came early.** The enemy flashed, lost health, showed its number and made its hit sound 683 ms before the knife arrived.
- **One knife for many hits.** SPRAY hit four enemies, but one knife flew.
- **No release.** The throw clip kept the knife in the rear hand and punched with the empty one.
- **A knife from nowhere.** The projectile appeared in front of the torso at about 5× the hand knife's size.
- **Coasting.** The knife arrived at 33 % of its launch speed.
- **One contact frame for every clip.** Every clip of every champion contacted on frame 5 of 8, with equal frame lengths.
- **Early ending.** The clip cut to a random idle frame before the knife landed.
- **Scattered clocks and generic sound.** Presentation ran on five clocks, using three generic sounds.

## Decision

1. **Contact is the beat.** For a damaging projectile, the fight's event timestamp is the moment the object lands.
   - `ActionPerformance.Schedule` starts the clip at `beat − travel − time-to-release`, so the release lands `TravelMs` before the beat.
   - A late start compresses only the clip's **elastic** frames before the release. The release is never slowed.
   - **The action handoff** (2026-09-25, `ActionHandoff`, `ActionClipTiming.FitRecovery`). An action whose blow has landed hands the figure over by ARRIVING at its exit pose. It is never cut mid-pose.
     - **Phases.** Every committed clip carries its phases. A plain strip is built by `ActionClipTiming.Plain`: contact 5, follow-through 6, recovery and exit 7, then the settle held on 7. Anticipation, commit/release, contact and the immediate follow-through are protected. Only the recovery and the settle may be borrowed from.
     - **The reservation.** The replay knows the next action. Its IDEAL start is its whole wind-up. Its LATEST start is its tightest fit: an authored clip's elastic floor (`ActionPerformance.MinLeadMs`), or a plain clip at `MaxClipSpeed`.
     - **The fit.** Once the outgoing blow lands, its recovery is fitted to end at the ideal start. The settle gives way first. Then the recovery plays faster, down to a readable floor: two display frames per pose, at most 3× its pace. Past the floor, the next action starts later, inside its own elastic range (the approved late start). If the time is short, the exit pose and the first recovery pose are kept and intermediates are skipped.
     - **Not begun.** A plain clip that could not reach its exit pose before the next PERFORMED action's latest start is not begun (`yield` in the trace). Its hit, number, flash and sound still fire, as they already do for any beat that falls while the figure is busy. This only happens on the fastest builds.
     - **What it replaced.** The recovery cut handed SPRAY the figure at its latest start whatever the swing was showing. At a fast TEMPO the swing was still in its low lunge, so the Seeker jumped in one frame to SPRAY's ready pose. Before that cut existed, SPRAY committed at its release: no wind-up, and a ~100 ms flight.
     - **The timing model is unchanged.** The beat, the release (beat − 250 ms), the flight and every piece of gameplay feedback stay where the fight put them.
     - **A performed action behind: YIELD after the contact** (the fast-TEMPO policy, 2026-09-25, `HandoffFit.Yielded`). When the next action is PERFORMED and its latest start falls inside this action's protected frames, a squeeze would start it late, and it would enter mid-wind-up. So this action YIELDS at that latest start instead, which is never before its contact: its gameplay event has resolved, the rest of it is not shown (`ActionClipTiming.CutAt`), and the incoming action keeps its minimum readable anticipation from its first pose. A plain action behind still waits and starts late, inside its own speed range.
     - **The outgoing action runs on** (`HuntScreen._outgoing`). A performance that hands the figure over keeps updating and drawing until it is finished: its impact fades, its ticks sound, and a melee lunge finishes its way home. It used to be dropped the moment the next one committed, taking its impact with it.
   - **Melee actions put their contact frame on the beat** (`MeleeActionRecipe`, `MeleePerformance`). The anchor is the `contact` marker with no flight, so the fist lands on the beat and everything describing the hit meets on one frame. The `commit` marker plays the commit cue and the callout.
     - **The body is carried, the arm is never stretched.** The champion stands hundreds of pixels from the creatures. The reach is measured at the clip's start, from the fist's authored socket on the contact frame to the target's contact point (`ContactPoint`, 0.3 × 0.32 of its body). PRESENTATION ROOT MOTION then carries the figure: a small pull-back through the anticipation (`LungeBack`), the whole distance during the commit, accelerating into the blow (`LungeAccel`: the fastest spacing is at contact), a small overshoot (`LungeOvershoot`). The root offset is added to the champion's draw box, so the body, the effects on him and his sockets all follow; the fight's positions never move.
     - **The way home is a RETREAT** (second pass). A heavy blow has an explosive approach and a controlled withdrawal. The follow-through stays planted; from the recovery marker the body travels home over `ReturnShare` (0.76) of the recovery, never quicker than `MinReturnMs` (110 ms), in the retreat pose, with spacing `Retreat(u) = smoothstep(u)^ReturnEase` (1.3): it leaves slowly because the blow spent the momentum, is fastest mid-way, and settles in. The lift is a LOW BOUND (`HopHeight` 0.018 of his height, ~7 px) only through the middle of the retreat (`BoundFrom` 0.15 to `BoundTo` 0.85), so he has landed before the exit pose shows. His shadow stays on the ground. The first cut was 0.62 of a 220 ms recovery (136 ms) with a 0.06 hop (25 px) over the whole return: a spring back, not a body recovering. A planted pose moved 400 px is a slide; a large parabola is a spring.
     - **The way home is owned by the performance.** It reads the timing its clip actually played (`IActionPerformance.Retime`), so a compressed recovery brings the body home sooner. A squeezed one, or a yield, carries the rest of the return on under whatever the figure plays next, finished by the next action's release: never a one-frame snap.
     - **A signature action belongs to its SKILL** (second pass, `ActionRecipes.For`; an ACCEPTED CONTRACT, 2026-09-25: every later recipe resolves through it, and a presentation shared by a Form must be declared at the Form tier, never inherited because two skills say the same ClipKey and FxKey). The first build resolved HARD HANDS' recipe from the Seeker's Strike Form and its `strike` effect, and BLOW (`hammer_blow`) says the same two words, so BLOW was PERFORMED AS HARD HANDS: its lunge and root motion, HARD HANDS' commit and hit cues, its impact look, its 0.55/120 ms target flash, its 0.45 duck from the commit, its callout at the commit instead of the beat, the suppression of BLOW's own strike effect, cast breath and hit, and a place in the handoff as a performed action (`ActionWeight.Signature` came with it too, but no code reads that field). The lookup is now, most specific first: (1) the skill's own recipe, keyed by the champion and the stable `SkillDef.Id`; (2) the champion's FORM recipe, deliberately shared by every skill of that Form whose effect it names (`EffectKeys`: SPRAY is the Seeker's knife volley, `projectile`, and WEEP's `weep` is not it); (3) none: the Form's plain clip, as it always was.
     - **A skill's own recipe plays its own strip** (`IActionRecipe.ClipKey`). HARD HANDS' hammer-fist is `char_seeker_hard_hands`; the Strike Form's strip is BLOW's knife swing again, byte for byte the art it had before HARD HANDS. Written over the Form strip, the signature's poses played for BLOW WITHOUT its recipe: a leap in place, hundreds of pixels from the creature. When a recipe's own strip is absent the skill plays its Form's plain clip (the screen times a performance by the recipe's clip, `TryAuthored`; the figure's envelope lists the recipe's clip before the Form's, `ActorClips.ChampionStrips`). PRESS, a FIELD, never takes a beat: it never played the Strike clip at all.
   - Non-damaging actions may name another semantic marker.
   - There is **no** deferred-health or deferred-death buffer. Because contact is on the beat, the fight's feedback is already at the right time.

2. **Authored timing** (`ActionClipTiming`, `<strip>.clip.json`):
   - per-frame durations;
   - elastic flags;
   - markers (`anticipation`, `commit`, `release`, `recovery`, `settle`);
   - sockets per frame.

   **The follow-through outlasts the flight.** The release frame plus the follow-through frame equal `TravelMs` plus one or two 60 fps frames. The open hand is still reaching toward the pack on the frame the knives land, and the recovery starts just after. The SPRAY: 50 + 230 ms.
   - The first build ended the follow-through 67 ms before the contact, so the arm was already down when the blades hit.
   - Exactly `TravelMs` was tried next, and the arm dropped on the contact frame itself.

   A strip without a timing file keeps the old uniform 5/8 behaviour exactly. The clip's last frame is the idle's first pose, and the idle then **restarts from frame 0** (`_anim − _idleFrom`) instead of resuming at a random phase.

3. **Sockets** (`ActionSocket`, `ActorSocketMap`):
   - A socket is a fraction of the square strip frame plus the direction a held prop points.
   - It is converted with the `Src`/`Dest` of the exact `SpriteFrame` the draw resolves.
   - `RH_SHOT_SOCKETS=1` draws the live socket and every blade's first position.
   - A later bone rig replaces the lookup, not the recipe.

4. **The performance** (`ActionPerformance`, one at a time, on the fight **playhead**):
   - reads the struck enemies from the fight's own resolved events (`ActionTargets.StruckBy`);
   - draws the held bundle at the hand socket until the release;
   - at the release marker, starts one blade per struck enemy from the bundle's fan positions;
   - flies them with a near-constant thrown-blade profile (`ProjectileMotion.ThrowPosition`, 6 % departure shaping) that bows apart so the fan reads;
   - lands them all on the beat.

   It runs **before** the frame's events are crossed, so the blades land on the frame the hits are presented.

   **The release accent** is a short smear. It shows only the last 18 % of the hand's path from the coil (`SmearTail`), lifted over the head (`SmearLift`). It starts ON the release frame and fades over 90 ms. The first version drew the whole path from the coil, which ran through the head, and at combat size it read as a beam from the eye.

5. **Colour roles.**
   - The flying object is drawn in two layers: its **material** (steel, untinted, alpha-blended) and its **emissive** edge (Source-coloured light).
   - The trail, glint, sparks and contact are Source-coloured light.
   - The character motif is the object's shape: the Seeker's kunai-style throwing knife.

6. **Impact priority.**
   - A performed hit replaces the generic hit puff and the generic hit sound, because they describe the same blow.
   - The enemy's flash and number stay. The recipe sets the flash's strength (`TargetFlash`, 0.38 for a four-to-five-target fan), so the pack does not turn solid white.
   - The recipe also sets the flash's shape (`TargetFlashRise` 0, `TargetFlashMs` 130). A thrown blade has already arrived on its contact frame, so its flash peaks there and is gone in 130 ms.
   - The fight's usual flash rises over the first fifth of 200 ms, so a swing's white arrives inside the blow. That rise put the SPRAY pack's peak 40 ms after the knives and kept it grey for 200 ms, the largest change on screen after the hit.
   - The contact is directional: a hot slash carried through the target along the incoming line, a small flash, and slivers thrown mostly forward.

7. **One focal point.** While the champion is performing, an ordinary bite on him is drawn at 0.55 opacity. Its number is unchanged, and its sound is ducked (below).

8. **Audio** (`SoundBank.PlayFirst(keys, volume, pitch, pan, vary, lead)`):
   - Release and contact cues resolve from the most specific to the most generic: champion + action, then archetype, then the generic cue.
   - They are played by the performance at **its** release and contact, with a gentle pan (±0.3).
   - **One contact** sound plays for the whole fan. A fan of three or more adds at most two quiet **ticks** (`ContactTicks`, volume 0.2) at the OUTERMOST blades, 18 and 36 ms after the contact, panned to them. Five knives are heard as one contact with width, never as five impacts.
   - A performed cast plays no `sfx_cast` at the beat and no generic `sfx_hit`.
   - **The mix** (`DuckOthers`, `DuckTailMs`): from the release until 160 ms after the contact, every other one-shot plays at 45 % (`SoundBank.Duck`). That covers an enemy's bite, another skill's cast, a critical's cue and a death. The performance's own cues play as `lead` and are never ducked. It is a duck, not a mute, because those sounds are still combat information. `Game1.Update` resets the duck every frame, so leaving the hunt mid-throw cannot leave another screen ducked.
   - The SPRAY's own cues exist (`sfx_seeker_spray_release/_hit/_tick`, synthesized by `tools/asset-pipeline/make_action_sfx.py`). They are measured, but no one has listened to them.

9. **The callout at the release.** The skill's name is shown at the release: presentation only. The skill tile's pulse, the cooldown and every piece of state stay on the gameplay event.

10. **Key poses, not generated actions.** An important action's frames come from controlled key poses: PixelLab `animate_with_skeleton_v3`, where every frame is drawn from one reference image of the champion with the pose sent as a skeleton. They are assembled by `tools/asset-pipeline/v2/keyposes.py`:
    - with the champion's own idle transform (pixel-exact, so there is no pop at either end);
    - with a key done by the tool, not the generator;
    - with the timing file written beside the strip;
    - with every PixelLab job, accepted or rejected, recorded.

    Props are drawn deterministically (`seeker_knife.py`).

    **An authored clip is drawn as its idle** (`UiKit.ResolveFrame` `placeAs`, HARD HANDS). The key poses are placed pixel-exact onto the idle in frame space, but the renderer measured every strip on its OWN extremes: HARD HANDS' raised fist (row 117 against the idle's 114) and wide loaded stance (498 against 495) drew the whole action 0.8 % larger and 3 px lower than the idle, a pop at every join. An authored clip now takes the idle's scale and ground line; its own crop still decides what of its texture is drawn. SPRAY's extremes equal the idle's, so nothing about SPRAY changed.

    **A light ground is repaired, not regenerated.** HARD HANDS' passes came back on a light ground in the reference's own glint colour, which the generator also used to fill his face and sleeves. `keyposes.py` keys the ground from the border, transplants the reference's face onto the blank region nearest the pose's nose joint, gives other blanks his sleeve grey, and turns light edge pixels into his dark outline. The accepted sources are cached in the repository, because PixelLab's links expire.

## Consequences

- **Timing** (polish pass, measured from the trace; `tools/asset-pipeline/action_timeline.py`). The fight's beat is t = 0. Everything that happens on it is presented on the first 60 fps frame after it (+17 ms). That frame carries the knives, health, flash, number and contact sound together.
  - Clip start −583 ms, anticipation −500 ms, extreme hold −383 ms (150 ms).
  - Release −233 ms: the pose, the sound and the blades on one frame. The flight is 250 ms.
  - Contact +17 ms. The follow-through holds through the contact frame.
  - The recovery starts about two frames after the contact; the idle restarts at +333 ms. The clip is still 920 ms (90/110/150/50/230/90/90/110).
- **Cost** (re-measured in the polish pass: unchanged for four knives; a real five-knife cast peaks at 85 sprites and at most 50 effects-pass draw calls). A four-knife fan peaks at 60 sprites. At its busiest frames (contact: slash, slivers and flash for every blade)
  the effects pass rises from 16–17 draw calls to at most 39, which is +22 including the one extra Begin/End of its light
  pass. The cause is texture switches per blade. Drawing each layer across all the blades (all trails, then all
  heads) would bring that down to about +6; nothing does that yet, and 39 is well inside the "low hundreds" budget.
  Its per-frame path is loops over fixed arrays (the blades, their trail rings and particles are allocated once, at the
  release); its allocations were not separately measured.
- **Handoff** (measured, `…/handoff/`; `handoff` / `yield` lines in `RH_PRESENT_TRACE`):
  - normal TEMPO: no handoff happens, and the champion's frames are identical;
  - the fast fixture (RHYTHM + VOLLEY), 33 s: 25 handoffs (15 settle-only, 9 at the readable floor, 1 compressed to the ideal start), no squeeze, worst recovery compression 2.60× (a HARD HANDS recovery frame, 87 ms in 33 ms);
  - the fastest build (TEMPO trained to 60 as well), 33 s: the plain action just before a SPRAY yields (11 swings, and 6 of the 11 HARD HANDS casts), and SPRAY's own recovery compresses up to 1.74× into the next action;
  - every SPRAY on both builds began on its first pose, released at −250 ms (−233 on the frame grid) and landed on the beat.
- **HARD HANDS** (measured, `production/qa/evidence/hard-hands/`; `root` lines in `RH_PRESENT_TRACE`):
  - normal TEMPO, beat = 0: clip start −367 ms, anticipation −300, loaded hold −200 (deepest pull-back −16 px at −133), COMMIT −117 (the leap, the commit cue, the first speed line at −83), the body at the target by the beat (+399 px), and on the +17 ms frame the contact pose, the fist's impact, health, flash, number and the hit cue together; follow-through +83, furthest +411 px at +133 and planted until +183;
  - the RETREAT (second pass): from +183 the body leaves at −1, −5, −12 px per frame, is fastest at −45 px per frame at +333, lifts at most 7 px (+283 to +317), is landed at +383, shows the exit pose at +400 with 10 px to go, is home at +417 (234 ms) and restarts the idle at +517. The first cut: home at +317 (134 ms) with a 25 px hop (`production/qa/evidence/hard-hands/pass2/10_return_path.md`);
  - fast TEMPO: into SPRAY the recovery compresses to its readable floor (3.0×, 330 → 110 ms) and the body is home on the exit frame, the join continuous (feet +0.2 px, the ready pose's own head −6 px);
  - joins, through the renderer's own placement (`tools/asset-pipeline/action_joins.py`): HARD HANDS → idle moves the silhouette by less than half a pixel (as SPRAY → idle does). HARD HANDS → SPRAY now measures exactly as idle → SPRAY: the ready pose's own change. The old crouched exit popped the head ~40 px there;
  - the fastest build (TEMPO trained to 60), 33 s: the old plain HARD HANDS was not animated at all on 6 of its 11 casts (it yielded to SPRAY). Now all 11 are performed. 6 are followed by a SPRAY only 500 ms later, and those YIELD after their contact: the contact is on screen one or two frames, the follow-through is not shown, and the body retreats home under SPRAY's wind-up (no teleport: the join's centre moves 5 px). 3 compress into a swing; 2 squeeze into a swing, and their way home runs on at the 110 ms floor under the swing's first frames (630 px in seven frames, one body, no jump). The swings' yields to SPRAY (11) are unchanged. Before the yield existed, those six squeezed: SPRAY entered in the middle of its hold, and the body jumped 523 px home in one frame;
  - BLOW (woven on the Seeker) and PRESS, after the isolation: BLOW plays its own plain Strike clip with its generic cast effect, breath and hit, with no root motion and no HARD HANDS cue; PRESS takes no beat, and no HARD HANDS cue, impact or root motion falls outside a HARD HANDS beat;
  - joins that no pair of these actions can reach whole at that tempo are a demand problem, not a timing bug: SPRAY needs 470 ms of lead and HARD HANDS 243 ms after its contact, inside 500 ms.
- **Switches:**
  - `RH_ACTION_RECIPES=0` plays every action the old way, for old-versus-new films.
  - `RH_SHOT_NOVFX=1` and `RH_SHOT_NOCHAMP=1` are the review views.
  - `RH_PRESENT_TRACE=1` logs release, contact and the performance's cost; for a reaction, `reaction-spawn`, `reaction-snap`, `reaction-recoil-end`, `reaction-release`, `reaction-end` and `reaction-draw` (sprites, chain links, draw calls, bytes allocated), read by `tools/asset-pipeline/reaction_timeline.py --report`.
  - `RH_REACTION_RECIPES=0` presents every reaction the old way while the actions stay performed; with `RH_SHOT_SEED=<n>` (the fight's crit dice, `new Random()` otherwise) one fight is filmed with the layer on and off, and `tools/asset-pipeline/action_regression.py` holds every SPRAY / HARD HANDS moment equal.
  - `RH_REACTION_CALLOUT=1` restores the reaction's callout (the with/without comparison). `RH_SHOT_ENEMY=<health>,<bite>` pins a fight fixture's enemy baseline (JAWS' killing answer, the fatal bite).

## ADR Dependencies

- **ADR-006 (Draw must not consume input).** The performance advances in Update, before the frame's events are crossed. Draw only reads its state.
- **ADR-009 (the premultiplied additive blend).** Every emissive layer (edge, trail, glint, contact) draws through `VfxBlend.Light` in a `VfxBlend.PremultipliedAdditive` batch opened by `VfxPlayer.BeginLight`.
- **ADR-010 (the composite projectile).** Its layers (trail, glint, sparks, directional impact) are reused. Its size rule (a share of the caster's height) is replaced, for performed actions, by the prop's own pixel scale.

## Engine Compatibility

MonoGame 3.8.4.1. It uses only `SpriteBatch` (Deferred; one extra Begin/End per frame while a throw is live), `SamplerState.LinearClamp`, `GraphicsDevice.Metrics` (trace only) and `SoundEffect.Play` with pan. No new engine API. The timing files are plain JSON copied by the csproj.

## GDD Requirements Addressed

None directly: this is presentation, and the fight's rules and numbers do not change. It serves the art contract (`design/art/arena-art-contract.md` §3.8) and the owner's action-presentation brief (2026-09-24).

## Known limits (reported, not solved)

- **The hand is an edit.** The release and follow-through hands are PixelLab `edit_image_pixen` results, taken only inside a hand box (the edits also redrew part of the body, painted three knives and redrew the other arm). The edit drew bare skin. The whole hand is recoloured to his glove, as he is gloved in every other frame, idle included. A first build gloved only the back of the hand, so for 250 ms he wore a fingerless glove. At combat size it reads as an open, flicked hand. At 3× the seam between the edit and the pose is visible.
- **HARD HANDS' exit pose: resolved** (2026-09-25). The old strip ended in a low crouch, so the head rose ~40 px in one frame into SPRAY or idle. The rebuilt HARD HANDS ends on the idle's own first frame, reached through its retreat pose. The basic swing's exit pose is still a half-rise with the blade out: its blade disappears at the join, as it does into idle after every swing (a plain clip, not rebuilt).
- **HARD HANDS' source art is a repaired generation.** Its key poses are PixelLab generations, deterministically repaired (face, sleeves, outline, cloak): at 3× the transplanted face is the idle's face on a lunging head, invisible at play size and at true speed (an accepted source-art compromise, not worth a generation). Its two sounds are human-listened and accepted (`design/audio/seeker-hard-hands-audio-brief.md`).
- **BLOW is on its legacy presentation.** Isolated from HARD HANDS, BLOW on the Seeker is the plain knife swing where he stands, 350+ px from the creature, with the generic strike effect on it: the pre-ADR-011 look. It belongs to a later action pass.
- **At the fastest build, HARD HANDS before a SPRAY is mostly its contact: an INTENTIONAL GRACEFUL DEGRADATION** (accepted 2026-09-25). The shortened post-contact recovery (the yield after the contact, the compressed or squeezed retreat) is the fallback for a timeline that cannot hold both actions, NOT the authored normal presentation: at normal TEMPO HARD HANDS always plays its whole follow-through and retreat. The fallback keeps both actions' gameplay truth and SPRAY's anticipation; what it gives up is HARD HANDS' follow-through. The alternatives, a shorter SPRAY lead (an approved action) or not performing HARD HANDS at all (the old yield), were judged worse.
- **The basic swing is measured on its own extremes.** The `attack` strip is a plain clip: its headroom (117) differs from the idle's (114), so it is drawn 0.8 % larger than idle, and its low frames' lowest row differs. Every swing → idle join carries that, as it always did. It is the swing's own art, outside this pass.
- **PRESS never animates the champion.** An earlier revision of this ADR said PRESS played the Strike strip in place: it was wrong. PRESS is a FIELD (passive): it takes no beat, so it never plays a champion clip; its picture is its pulse (the FIELD/AURA task).
- **THE FAST-TEMPO PRESENTATION POLICY (owner, 2026-09-25).** The incoming authored action keeps its minimum readable anticipation: SPRAY never begins at its third frame, because its first frames are part of the approved action. If the outgoing action cannot complete its presentation in the time left, its presentation may YIELD, and only after its gameplay event has resolved (its hit, number, flash and sound still play). Yielding is a FALLBACK, not the presentation model: an action that yields often is an action whose protected phase is too long or whose recovery does not compress. Measured on the fastest build (TEMPO trained to 60), 33 s, before HARD HANDS was rebuilt: 17 yields (11 swings, 6 of 11 HARD HANDS casts). After: 11 swing yields, no HARD HANDS cast unanimated, and 6 HARD HANDS handoffs that yield after their contact (`HandoffFit.Yielded`).
- **Other actions.** Besides SPRAY and HARD HANDS, the Seeker's clips and every other champion are still plain strips (the old uniform model), handed off by the same rule. The old strips still disagree about the Seeker's weapon.
- **JAWS: a 0 ms armed window cannot be filmed on this Seeker.** The fight checks readiness on its 100 ms ticks and his rearm (2830 ms, 1583 ms at fast TEMPO) is not a whole number of ticks, so it always comes due between two bites. The 0 ms case (a rearm that IS a whole number of ticks, as a champion at ×1.0 or ×1.5 would have) is posed and proven by the Core tests and the replay test, not by a film.
- **JAWS' first trigger happens before a capture starts** (a film begins ~1 s in): the killing answer and the fatal bite are filmed in the SECOND run, after a fall restarts it.
- **The fight's dice are unseeded.** `Descent.Rng` is `new Random()`, so two captures agree on every beat and differ on every critical; comparisons need `RH_SHOT_SEED`.
- **JAWS' trap is DRAWN, not generated.** The first world art (a PixelLab U that shut into a toothed ring) read as a portal or a collar; the polish pass's drawn head as a metal crocodile; the identity pass's box and thin arms as a grabber with two hooks. PixelLab was used for three CONCEPTS only in the identity pass (`42696d59`, `7e776e95`, `4513dcd7`) and none in the readable-clamp pass; earlier `3c45cc40` (a robot crocodile) and `ec3db96a` (a vise) were rejected. The base and the two jaws are drawn on the Seeker's grid (`tools/asset-pipeline/v2/seeker_jaws.py`: the jaws' bands and teeth are polygons snapped to the grid).
- **Seeing the close is a timing contract.** At 60 fps a close must span at least three drawn frames to read as motion: the jaws' `CloseMs` (50) gives open, still open, half shut, SHUT. The screen's playhead runs in thirds of a millisecond, so the frame drawn at the stop reads 49.99 ms; the stop step allows half a millisecond so the answer lands on the frame that shows the jaws shut.
- **The identity was checked BLIND** (`jaws-identity/24_blind_semantic_check.md`): fresh model readers with no context, one image each, neutral names. The CURRENT head read as "chomping jaws / mandibles" with an eye; the final clamp reads as a device to all three readers (two name a bear trap, a clamp or a trap). A weak residue remains: an open C of two toothed arms reads a little mouth- or mandible-like to some readers in isolation; in play the caught limb fills the C. A proxy, not a playtest: the owner's look is the check.
- **JAWS' size is a share of the creature's height** (`ClampBodyShare` 0.40 of it, 52-96 px, the trap's HEIGHT from the jaws' top to the coil). Chosen at play size from 85 %, 100 % and 115 % (`jaws-readable/13`).
- **The held field.** The pink rectangle (`fx_press`, a weight slab the aura tinted) is line art now (`tools/asset-pipeline/v2/press_field.py`). Its once-per-second brightness pulse belongs to the FIELD/AURA gold-standard task.

## Alternatives rejected

- **Release on the beat, feedback held until contact.** This was the discovery's first proposal. The owner chose contact on the beat, which needs no deferred health, death or number buffer.
- **An eight-frame generated throw.** This was the old clip. It is what put the knife in the wrong hand and punched with the other.
- **Drawing the knives into the character frames.** The flying object could then never be guaranteed to be the same object at the same size. The prop is a runtime sprite at the hand socket, so it is.
