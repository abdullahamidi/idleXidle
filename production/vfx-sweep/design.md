# Remaining-skill sweep: presentation design (2026-10-03, revision 2)

Written by the combat presentation director for the programmers who build the sweep. It covers the 34 items of
`manifest.md` on the infrastructure described in `infrastructure.md`. The five closed references (SPRAY, HARD HANDS,
JAWS, PRESS, BRAND; ADR-011, ADR-013) are the quality bar and the principles; nothing here copies their art. Every
decision below is made; none is a question for the owner. The owner sees the result once, in the ONE showcase film
of section 9. Revision 2 absorbs the adversarial review; the Critique log at the end says what changed and why.

Vocabulary used throughout:
- **the beat / the tick**: the fight's own event millisecond. Everything that describes a hit meets on the first
  60 Hz frame after it (ADR-011). Nothing presented ever moves health, a death or a number away from it, except the
  two explicit echoes allowed in section 4 (heal return, reaction answers), which only delay a NUMBER or a shown
  fall, never the bar.
- **Source light**: the slot's Source colour (`SourceGlow`: Body E84A5E, Mind 5AC8E8, Nature 7FCB4A, Machine E88E3C,
  Shadow 9B7BFF, Spirit E6E0FF), drawn through `VfxBlend.Light` in the additive pass (ADR-009). Light is never
  the object: props and materials (steel, stone, bone, wood, iron, dust) are untinted and alpha-blended.
- **quiet grade / skill grade / major grade**: three number styles. Quiet = half size, 70 % alpha, no outline (ticks,
  bleeds, carries). Skill = today's skill-graded number. Major = skill size with a Source outline and a one-word
  caption (FINISHED, CARRIED).
- **the focus window**: -200..+350 ms around an action's contact, -700..+400 around a presented reaction, the same
  ms as a PRESS crush. Inside it, lower-tier pictures draw at x0.6 and their cues take the duck (PRESS's and BRAND's
  quiet rule, generalised in section 3).
- **a build**: at most 2 Active + 2 Passive skills (`Build.MaxActiveSkills` / `MaxPassiveSkills`; a passive is any
  Field or Reaction, `TakesABeat => Kind == Active`). So a fight never holds more than TWO fields or reactions, and
  every pose in section 9 declares its full four slots.

---

## 1. Classification by gameplay semantics

Archetypes: **MELEE** (HARD HANDS), **PROJECTILE** (SPRAY), **REACTION** (JAWS), **FIELD** (PRESS), **AFFLICTION**
(BRAND). Six derived conventions (section 4): **SELF-STATE** (a state on the hunter), **RETURN** (something comes
back to the hunter), **SHIELD**, **DROP** (a fall from above on a clock), **EXECUTE**, **CARRY**. Two small
performance kinds are added for semantics no closed class covers: **GESTURE** (a cast clip with a commit marker and
no contact: CALL, REPAY, PULSE) and **DROP** (SLOW FALL, HELD NOTE). MeleePerformance gains a **REACH** mode (no root
motion; the contact is bridged by a streak: DRINK, the Oathbound's lash).

| # | Item | Primary | Secondary / phases | Presentation work? |
|---|---|---|---|---|
| 1 | hammer_blow BLOW | MELEE (Signature weight) | FINISH -> EXECUTE; BREAKTHROUGH / CLEAN CUT -> CARRY; TRAIL -> SELF-STATE (armed hand); FLATTEN -> guard-break accent | yes |
| 2 | snare_repay REPAY | SELF-STATE (the bank on the hunter) -> GESTURE + DISCHARGE (the bank snaps from his shoulder to the front creature, arriving on the beat) | BANKED -> SHIELD (the bank becomes a plate); CARRIED -> SHIELD at wave open | yes; needs Core hook C1 |
| 3 | sign_call CALL | SELF-STATE (an open window on the hunter's output) | GESTURE (no contact); the EMPOWERED-HIT accent on the FIRST amplified hit and on SPEND's spends; STEADY = levels | yes; needs Core hook C3 |
| 4 | volley_weep WEEP | AFFLICTION (the bleed pool standing on the front creature) | a death-sourced TRANSFER (corpse -> new front), ONSET = cast -> pool feed | yes; needs Core hook C2 |
| 5 | field_pulse PULSE | GESTURE + a RADIAL GROUND SHOCK (a flat ring from his stomp, every struck creature flicked on the beat) | per-target contact for SHARE / NARROWED / BALANCE | yes (new kind: Shock, ground-plane) |
| 6 | field_mire MIRE | FIELD (a ground field under the ENEMY row, ticking) | AFFLICTION (the pack sunk; depth = the slow; the feet occluded, never displaced) | yes |
| 7 | drain_drink DRINK | MELEE in REACH mode (a grab-at-reach on the front creature, no root motion) | RETURN (life back to the hunter on the Heal); GLUT = a fed blow, no return | yes |
| 8 | drain_wilt WILT | AFFLICTION (a wither on the bodies, depth = the attack break) | FIELD cadence (ticks); SUP = quiet RETURN | yes |
| 9 | sig_anvil_hardface HARDFACE | MELEE (Signature) | AFFLICTION (the kill-strip cracks on survivors, a dark ground ripple from the corpse); UPSET = a ground slam with a dust skirt | yes |
| 10 | sig_chorus_grave_song GRAVE SONG | FIELD (the song is everywhere: nothing travels; the dead hum, the living shiver on the tick) | intensity from the dead count; CHANTRY = wave-wide hush AFFLICTION | yes |
| 11 | sig_metronome_clockwork CLOCKWORK | PROJECTILE (one bolt per struck creature, contact on the beat) | a clock telegraph (SELF-STATE, last 600 ms); HELD NOTE = one DROP over the whole row | yes |
| 12 | sig_unbroken_hold_fast HOLD FAST | FIELD on the hunter (a plate set on each tick) | SHIELD grammar claimed at its tick; at the cap a silent rim pass; DEEP ROOTS = RETURN from the ground | yes |
| 13 | sig_tower_slow_fall SLOW FALL | DROP (a stone falls on the front creature on its clock) | FIELD timing; ONE STONE = one slab over 2-3 | yes (new kind: Drop) |
| 14 | sig_quiver_backdraw BACKDRAW | REACTION (death-triggered) -> PROJECTILE (arrows to each struck survivor) | numbers / falls echoed to arrival | yes |
| 15 | sig_thornwall_narrows NARROWS | REACTION (bite-triggered, the answer on the front creature) | growth tier from the per-wave answer count | yes |
| 16 | sig_oathbound_oathmark OATHMARK | REACTION (bite-triggered opening) -> AFFLICTION (a sworn band on each covered creature for the window) | the EMPOWERED-HIT accent only where a band claims it | yes; needs C3 |
| 17 | sig_magpie_paying_work PAYING WORK | MELEE (Signature; a bigger cut on a boss, still T3) | RETURN (the loot flies back on the Heal); LIGHT FINGERS = touches on the rest | yes |
| 18-27 | form:* (10 basic attacks) | MELEE Ordinary with a 20-25 % step-in (seeker, thornwall, magpie, anvil, metronome, tower); PROJECTILE Ordinary (quiver, chorus, unbroken); REACH Ordinary (oathbound) | per-champion hit-cue family; FIRST BEAT / MOMENTUM / DEADWEIGHT accents | yes (the most-seen item of the game) |
| 28 | proc:heal | RETURN (generic receive) | claimed by a recipe when the heal is that cast's | small: replace the column with a chest glow; throttle "+N" |
| 29 | proc:shield | SHIELD grammar (gain / absorb / break / barrier) | - | **not presentation work**: coherent, dedicated cues, soft art. Only change: a recipe may CLAIM the ShieldGained at its own ms (HOLD FAST, BANKED) so the generic gain does not double it |
| 30 | proc:undying | Major self-save (RETURN of the figure from a fall) | - | small: own cue, a collapse-in flash; the gold stays (it is the keystone's ink, not a Source) |
| 31 | proc:deadweight | AFFLICTION (a stored weight on the creature) + a release accent | - | small, very quiet |
| 32 | proc:weaver_echo | infrastructure: an ECHO mode on every performance | - | infrastructure, not art |
| 33 | proc:bleed_reflect | AFFLICTION (the pool, shared with WEEP) + a 1-sprite reflect spark on the biter | - | generic-path fix (no strobe) + WEEP's layer shows the pool with VENOM's provenance |
| 34 | proc:charge | HUD state | a REND dump = the EMPOWERED-HIT accent | **not arena work** beyond the accent: the HUD chip is the truth; no orbiting pips |

Also flagged, not presentation: REPAY CARRIED's card says 10 % while the dial is 0.05; DRINK SIPHON's card promises
the per-wave ceiling doubles while the variation only doubles Lifesteal. Report both to the content owner; the
pictures below present what Core does. BACKDRAW's phantom Skill on the wave's last kill is skipped by presentation
(a Skill with no following Strike in its batch draws nothing); a Core alive>0 gate is recommended separately and is
not required.

**Core information-only hooks** (each fingerprint-proven like `Marked` / `ReactionArmed`: ten builds over three waves
resolve byte-identical with the report on and off):
- **C1 `Banked`** (REPAY): Slot = skill slot, Amount = the windowed bank after this bite, at the bite ms; and
  Amount = 0 at the cast ms after the bank is spent. VENGEANCE / RAW NERVE windows are not re-derived on screen: the
  picture shows the last reported value and fades it cosmetically over the def's window dial.
- **C2 `Pooled`** (WEEP / VENOM / crit-to-bleed / overkill-to-bleed): Slot = provenance (the feeding skill slot, or
  -1 for a keystone), Amount = the pool after the change, at the change ms. Emitted on growth only; ticks already
  print their Strikes.
- **C3 `AmpWindow`** (CALL / OATHMARK / MIND stretch / FOUNDATION): Slot = skill slot, Amount = the ABSOLUTE ms the
  window now closes at, emitted on every CHANGE of `ampUntil` / `ampBonus`: the open, a refresh, the MIND stretch
  (`SoloBattle.cs:2980-2985`, a real change) and SPEND's count-close (`ampUntil = 0` inside the hit, Amount = AtMs).
  A TIMEOUT is not a change (`ampUntil` is only read lazily at the Amp read, `:1411`), so nothing is emitted for it:
  the screen ends the state itself at the last reported close ms. The existing `Marked` keeps carrying the percent
  at the open. **C3b `Spent`**: Slot = skill slot, Amount = charges remaining, on each SPEND spend. FOUNDATION's
  primed stack: an `AmpWindow` at wave ms 0.
- Nothing else. HARDFACE (Break / DefenceNow), MIRE (Slowed), WILT (AttackBreak), HOLD FAST (Aura / ShieldGained),
  SLOW FALL (Aura + Strikes), GRAVE SONG (EnemyDown count), NARROWS (count of its Skill events this wave), CLOCKWORK
  (the next Skill event is already in the replay), DRINK (Heal same ms) are all readable from the replay.

---

## 2. Cohorts

### A. Basic attacks (form:* x10) - Ordinary tier
Grammar: ONE verb per champion, the contact AT the creature on the beat, one hit cue, a small directional impact,
no release cue for melee, no callout, never a duck. A melee basic STEPS IN 20-25 % of the row gap (the champion's
own "bite", the mirror of ADR-012's whelp lunge at ~20 % travel): the offset is a curve over the clip's own frames
(as `BitePresentation`'s lunge), peaking on the contact frame and back by the settle frame; it is NOT root motion
to the target and has no retreat phase, so nothing can be left away from home at any TEMPO. The contact is read
from the flash, the number, the cue and a small contact picture AT the creature. Full root motion is a signature
privilege (HARD HANDS, BLOW, HARDFACE, PAYING WORK). Projectile basics never move the body and release their missile
200 ms before the beat; the Oathbound's lash is a REACH (section 1) and never moves the body.
Consistent: timing model, impact size (<= 0.35 of the creature's height), TargetFlash 0.30 / 110 ms, hit cue
0.34-0.38, number plain grade, ImpactWeak puff and the 40 px post-hit push REMOVED.
Varies: the weapon (the strip's own action), the missile prop, the hit-cue material, the impact's shape (a cut, a
thump, a thud, a snap).
Failure modes: a projectile basic needing its clip to start one flight early every beat (the elastic frames absorb
it); baked VFX in four strips (thornwall, magpie, chorus, unbroken) fighting the Source light.
Identity: the Seeker's blade cut, the Anvil's and the Metronome's fists, the Tower's hammer, the Thornwall's shield,
the Magpie's dagger, the Quiver's arrow, the Chorus's bone charms, the Unbroken's flung stone chip, the Oathbound's
chain. Source light only on the edge and the contact.

### B. Heavy melee skills - Skill / Signature tier
BLOW, HARDFACE, PAYING WORK (root motion) and DRINK (REACH). Grammar = HARD HANDS: a MeleeActionRecipe on the
skill's own strip (the Form strip where no signature strip exists), authored commit / contact / recovery markers,
the contact frame on the beat, a commit cue and a hit cue, the duck from the commit to +160 ms, callout at the
commit, impact priority (no cast breath, no generic thud, no puff).
Consistent: the five phases, the retreat home (root-motion recipes), the exit pose = idle frame 0, one hit cue for
the whole blow.
Varies: weight (the Anvil is slower and heavier than the Magpie), the impact's character (weight = flat ring + dust +
buckle; cut = directional slash + slivers; grab = a contracting ring), what follows the contact (EXECUTE, CARRY,
RETURN, soften).
Failure modes: BLOW leaking HARD HANDS' recipe (BySkill isolation, pinned); two signatures sharing a Form strip
(allowed: a strip's `.clip.json` belongs to the strip, the recipe owns the look and cues); a heavy blow every beat
at fast TEMPO (yield after contact, as accepted).
Identity: the champion's own strip and weapon; the hit cue's material (padded fist, hammer on timber, knife in
leather, a wet grab).

### C. Projectiles - Skill / Signature tier
CLOCKWORK, BACKDRAW's arrows, the three projectile basics, SPRAY on other champions. Grammar = SPRAY: one object per
struck creature, composed at runtime (ADR-010: a prop head part, a runtime trail, a glint, a directional contact),
release at a marker from a hand socket, contact on the beat, one contact cue + <= 2 outer ticks.
Consistent: the composite, the fan, the flight profile, the contact hierarchy.
Varies: the missile (each champion owns ONE: section 6), its scale, travel (250 ms skills, 200 ms basics, 180 ms
reactions), the cue material.
Failure modes: a static strip flying (never again: no strip projectile is spawned by any live profile after the
sweep); the fan assigned to targets Core did not strike (ActionTargets.StruckBy is the only source); OVERDRAW /
ECHO adding passes on one ms (one fan, the echo quiet). REPAY is NOT in this cohort: a thrown mass on the champion
the film shows SPRAY on is SPRAY with a ball; its discharge is a strand (5.2).

### D. Reactions - Signature tier (JAWS level)
NARROWS, BACKDRAW, OATHMARK's opening, WEEP's transfer, JAWS on other champions. Grammar = JAWS: a layer that never
takes the figure, anchored on the trigger's ms (the bite's contact or the shown fall), a short phrase (~300-700 ms)
with ONE cue on its snap, the answer's number and a killed creature's fall echoed to the snap (`ReactionEcho`,
`_deathDeferred`), quiet while the champion performs, ducked 0.45 under an action, no callout.
Consistent: the echo discipline, the quiet rule, the layer order (behind / over the creature as the phrase needs).
Varies: the phenomenon (thorn stakes closing, arrows loosed, an iron band clasping, blood leaving a corpse).
Failure modes: the same-frame clash with the bite's own lunge and thud (keep the bite's thud; the answer's cue is
later and different in material); cascades under LOOSE AGAIN (one phrase at a time per layer; a second trigger inside
a phrase joins it quietly: no restart).

### E. Fields - Skill tier (PRESS level)
MIRE, WILT (its cadence), GRAVE SONG, HOLD FAST, SLOW FALL, PRESS and BRAND on other champions. Grammar = PRESS's
PRINCIPLES, never its picture: a layer built per wave from the Aura ticks and the events at the same ms, the tick
is the arrival, a quiet persistent presence that compresses before the tick, one tick cue (<= 0.34, never lead),
x0.6 inside a focus window, giving way to a reaction on a shared tick, never a committed clip. PRESS's defining image
(a crescent front leaving the hunter, travelling LEVEL along the row, arriving on the tick) belongs to PRESS: no
other field or shock sends a front down the row, faster or brighter is a parameter, not a different picture.
Consistent: tick-anchored motion (the wall-clock pulse is deleted), the quiet rule, the cue ceiling, one number
per creature at quiet grade for damage ticks.
Varies: the SUBJECT (the enemy row's ground, the bodies, the dead's places, the hunter, the sky above the front
creature), the material.
Failure modes: FieldRoles' one-Performed / one-Held arbitration (rebuilt, section 8; a build holds at most TWO
fields, so `FieldLayers` is a list of <= 2); two fields buckling one body on a shared tick (PRESS's crush owns the
squash; others skip theirs); heals every tick (claimed, "+N" throttled).

### F. Afflictions - Quiet tier (BRAND level)
WEEP / VENOM pool, WILT wither, HARDFACE cracks, OATHMARK bands, MIRE sink, DEADWEIGHT, CHANTRY hush.
Grammar = BRAND's principles: change the victim (tint, squash, a mark seated at the authored body point), quiet at
rest, stronger on state change, depth shown structurally, host-adaptive contrast, coexists with everything, 0 bytes a
frame, event-driven audio and mostly silence.
Mechanism: ONE `AfflictionLayer` (section 8) with per-creature slots, drawn in the creature loop after the creature
and before its hit flash at `MarkPoints` (the `.mark.json` body points already exist for all 60 idle / attack and 30
death strips), plus two hooks the arena already has the shape of: a draw TINT and a SQUASH per creature (as the curse
has `PassDue`). Neither hook moves a draw box: ADR-006, Update and Draw read one geometry, and hover, click hitboxes
and the inspector read the actor box. BRAND's shader pass is not touched and not reused: territories are BRAND's
identity.
Consistent: the body point, the depth ladder (1 / 2 / 3 visible steps), the quiet rule, the migration by the shown fall.
Varies: the material (drips, cracks, an iron band, a dull lump, a withered tint, a hush).
Failure modes: two afflictions on one body point (the layer offsets by kind: tint and squash are whole-body, drips
hang from the point, cracks sit above it, a band circles it, a lump sits below it); a UI symbol creeping in (no icons,
no reticles, no rune discs).

### G. Self-states and sustain - Quiet / Skill tier
CALL, REPAY's bank, OATHMARK's window (shown on the enemies, section F), HOLD FAST, the shield grammar, heal returns,
UNDYING, CHARGE. Grammar: a state the hunter carries is drawn ON or AT him (behind him in the light pass, or at a
hand socket), it is QUIET AT REST (it breathes on the skill's own ready ping, or on every 4th Beat event when the
skill has no ping; never per beat, never a wall clock), it visibly ENDS, and what it does to his hits is shown on
the hits (the EMPOWERED-HIT accent, at countable moments only) so a shown property does something. Returns travel
from the cause to his chest and arrive at a fixed +280 ms; the bar is the truth and never waits.
Failure modes: strobe on 5-beat refresh (a refresh re-lights without restarting the open); three states on one
figure (CALL's glow behind, REPAY's embers at the guard shoulder, the plate in front: three places, never one); a
state at its cap that keeps performing (HOLD FAST at the cap: one silent rim pass, nothing drops).

---

## 3. Intensity hierarchy

Weights map to `ActionWeight` (Quiet, Ordinary, Skill, Signature, Major); every recipe declares one and the tests
pin the cue volume against the tier ceiling, as `brand_audio_test` pins BRAND under PRESS.

| Tier | Items | Visual weight | Flash | Screen space | Duration | Audio (x master 0.8) | Mix |
|---|---|---|---|---|---|---|---|
| **T0 Quiet** (persistent states, affliction ticks) | WEEP / VENOM pool, WILT, HARDFACE cracks at rest, OATHMARK bands at rest, MIRE at rest, DEADWEIGHT, CHANTRY hush, CALL's standing glow, REPAY's embers, CHARGE | <= 3 sprites a host, <= 0.3 of the body, no white, alpha <= 0.5 | none | the body only | continuous, motion <= 1 step / 800 ms | 0.09-0.22, event-driven only (BRAND's band), never a loop | always ducked, never lead |
| **T1 Ordinary** (routine hits) | the 10 basic attacks, bleed / reflect / carry / deadweight ticks' numbers, the EMPOWERED-HIT accent | impact <= 0.35 of the creature's height | 0.30 / 110 ms (ticks: none) | the contact point | <= 250 ms | hit cue 0.34-0.38; ticks silent | never ducks others, takes the duck |
| **T2 Skill** (equipped actives, field ticks) | PULSE, DRINK, REPAY, CALL's open, MIRE / WILT / GRAVE SONG / HOLD FAST / SLOW FALL ticks, OATHMARK's open, SPRAY-agnostic | impact <= 0.6 of the height; a ground ring may span the row at <= 0.12 of a creature's height tall | 0.38 / 130 ms | arena-local (one creature or the struck set) | <= 500 ms after the beat | contact 0.40-0.50 (SPRAY's 0.50 is the ceiling); field ticks <= 0.34, never lead; a tick faster than 1.5 s <= 0.24 | an ACTIVE ducks 0.45 from release to +160 ms; a FIELD never ducks, takes x0.6 in a focus window |
| **T3 Signature** (the champion's defining action, reactions) | BLOW, HARDFACE, PAYING WORK (boss included), CLOCKWORK, NARROWS, BACKDRAW, JAWS-agnostic | impact <= 0.8 of the height; the body carried; a 2-frame contact hold allowed; NO white-hot frame | 0.50 / 120 ms (reactions 0.25 / 90) | arena-local; a reaction may use the ground under the target | <= 700 ms | commit 0.28-0.34, hit 0.52 (HARD HANDS' 0.55 is the ceiling); reactions 0.42 (JAWS') | ducks 0.45; reactions take the duck |
| **T4 Major** (once per wave or run, state-changing) | FINISH's execute, UNDYING, a shield break (existing 0.58) | one white-hot frame allowed; impact <= 1.0 of the height | 0.65 / 140 ms + 1 frame white-hot | arena-local; NO screen shake, no vignette (the art contract has none) | <= 400 ms | 0.56-0.62, lead | ducks 0.35 for 200 ms |
| **T5 Boss-rare** | boss down (existing) | unchanged | - | - | - | 0.46 | - |

Rules that cut across tiers:
- **Ceilings are the references'**: nothing contacts louder than HARD HANDS' 0.55; no field ticks above PRESS +0.06;
  no affliction cue above BRAND's apply 0.183 x 1.3 = 0.238 (so 0.22 is the practical cap).
- **T4 is rare by definition.** Anything that fires on a cadence (every cast of a boss wave, every tick) is T3 at
  most. The white-hot frame is JAWS's snap language and the execute's; nothing on a cadence gets it.
- **One cue per event, one event per cue.** A multi-target contact is ONE cue + <= 2 ticks at 0.16-0.20. N creatures
  never make N sounds.
- **A tick never flashes.** Field and affliction ticks move the body (ripple, shiver, droop) and print a quiet number;
  only ACTIONS and REACTIONS flash.
- **The quiet rule** (PRESS / BRAND): inside a focus window a T0-T2 picture draws at x0.6 and its cue at x0.6 (the
  duck alone when already ducked); a field tick sharing its ms with a presented reaction gives way (x0.5, no squash).
- **Same-ms ownership**: a Strike belongs to the MOST RECENT Aura or Skill event at its ms. The field fork's Aura and
  Strikes precede a cast's Skill and Strikes on a shared ms (`SoloBattle.cs:2240-2270`, then `:2489`), so "the last
  owner" is exact: `auraAtMs` in `HuntScreen.cs:2215-2234` becomes a last-owner (kind, slot, ms). A PULSE cast on a
  MIRE tick's ms keeps MIRE's Strikes as MIRE's and PULSE's as PULSE's.
- **Callouts**: ACTIVES say the skill's one-word name at the commit / release (BLOW, REPAY, CALL, PULSE, DRINK,
  HARDFACE, CLOCKWORK, PAYING WORK); reactions, fields and afflictions say nothing (JAWS / PRESS / BRAND). The style
  word (HAMMER, SNARE, SIGN, FIELD, DRAIN, VOLLEY) is gone from the arena.

---

## 4. Derived conventions

**SELF-EMPOWERMENT (CALL; STEADY; the EMPOWERED-HIT accent shared by OATHMARK, REND, FIRST BEAT, TRAIL).** The state
lives in two places. On the hunter: a soft Source light behind his upper body in the light pass (`fxp_flash_soft`,
peak alpha 0.35, 0.9 of his height, centred at his chest), quiet at rest (it breathes +0.1 on the skill's ready ping,
never per beat) and goes OUT over 200 ms at the close (the C3 close ms, or the reported count-close). On the hits:
the PRIMARY signal of an amplified hit (HitSource Primary or Swing inside the window) is its NUMBER drawn with a
Source outline, plus a thin Source rim on the hit flash. The RING accent (`fxp_ring_soft` growing 0.3 -> 0.45 of the
creature's height over 110 ms, peak alpha 0.3, under the contact) is drawn ONLY at countable moments: the FIRST
amplified hit after an open or refresh (it proves the window is live) and each SPEND spend. Never on every hit: under
CALL base that would be every hit for 6 s, under STEADY every hit of the wave, and at fast TEMPO the most frequent
large picture on screen. Under SEALED WORD only banded creatures are amplified (`ampWholeWave || front`,
`SoloBattle.cs:1413`): the accent is drawn only where a band or the glow claims it, never from "inside the window"
alone. Silent: the hit's own cue is the sound. SPEND turns the glow into N motes (3 / 5) orbiting the raised hand; a
spent hit sends one mote along the hit (it leaves at the Strike ms, 120 ms flight, dies in the ring); a PERFECT
CLAUSE crit flares its mote and keeps it. STEADY shows levels: the glow at 0.2 (one stack) and 0.35 (two);
FOUNDATION starts the wave at level 1.

**RETURN (DRINK, PAYING WORK, TRICKLE, SUP, DEEP ROOTS, the generic heal).** Something travels from the cause to the
hunter's chest and ARRIVES at +280 ms after the Heal event. The bar moves on the event (truth); the "+N" text and a
chest glow (`fxp_flash_soft` Nature-or-Source, 0.25, 300 ms) wait for the arrival. The stream is 3-5 motes
(`fxp_spark_dot` + `fxp_trail_soft`) on an arc, count from the healed share of max health (1 mote < 1 %, 5 motes >= 6 %).
No Heal event (ceiling spent, NoHealing, GLUT): no stream, no text, nothing faked. The generic receive (no recipe)
is the chest glow + "+N" with no stream, "+N" summed over 400 ms. The intake cue is 0.22, only when a recipe claims
the heal. `fx_heal`'s column is no longer spawned.

**SHIELD (HOLD FAST, BANKED, CARRIED, the shield grammar).** The existing grammar stays: gain = rim flare +
`sfx_shield_gain` 0.40 + the one-shot; absorb = flare + 0.26; break = 0.58 + `fx_shield_break`; the barrier held
while shield > 0. A recipe CLAIMS the ShieldGained at its own ms: its own picture and cue replace the generic one-shot
and `sfx_shield_gain`; the bar rim flare stays. A wave-open gain (GROUNDWORK, CARRIED) is shown silently: the barrier
is simply up at ms 0, no callout.

**DROP (SLOW FALL; HELD NOTE).** Something arrives from ABOVE on a tick it could not have been thrown on. Telegraph:
a soft dark disc (`fxp_flash_soft` in near-black, alpha-blend, untinted) under the target grows from 0.2 to 0.8 of
its width over -450..0 ms. The object enters from above the arena at -150 ms accelerating (gravity: position =
(t / 150)^2), lands on the target's body point on the tick. Contact: dust, a flat ring, a buckle (`UiKit.Buckle`
x0.86 / x1.08, 90 ms), the number, the fall on time. The object fades over 250 ms (no litter). The target is the
tick's own Strike slot, resolved when the drop is scheduled from the pre-read events (a creature that dies between
the telegraph and the tick is not a problem: the Strike names who was hit). No travel sound: the shadow is the
warning. UPSET does not borrow this: it is a ground slam (5.9).

**EXECUTE (FINISH, BRINK, TWICE; the finishing bonuses of COURSES / CLEAN SWEEP / LIGHT FINGERS under a threshold use
the accent only).** The executed creature (a Strike at the same ms as the blow, on another or the same slot, that
kills) takes a MAJOR beat distinct from the blow: a tall thin vertical cut-flash (`fxp_flash_soft` squashed 0.15 x 1.2
of its height, dark Source for one frame then white-hot for one frame), its fall on time, its number at major grade
captioned FINISHED, cue 0.56 lead (a single heavy, clean impact with a short dark tail; no crack). Threshold-only
bonuses (not a kill rule) use the EMPOWERED-HIT number outline in white instead.

**CARRY (BREAKTHROUGH, CLEAN CUT; every HitSource.Carry).** Core carries instantly and never carries twice, so the
picture is instant too: ONE bright 2-frame Source line (`fxp_trail_soft`, no motes) from the dying creature's body
point to the next creature's, and the carry's number ON THE BEAT at quiet grade captioned CARRIED; its flash is the
arrival's (0.30 / 110), silent (a 0.16 tick only when the carry kills). No 120 ms echo: the number and the health
change stay on one frame. Two numbers on one frame on two creatures is intended.

**ARMED NEXT-SWING (TRAIL; and the GUARD-BREAK accent of FLATTEN / PRISED OPEN / STEADY HAND / SHROUD / COURSES).**
Armed: the weapon hand's socket carries a small white-edged Source glint (`fxp_glint_star`, 0.12 of his height) from
the blow until the next swing's release. Guard-break (an armour-ignoring hit): 4-6 dark slivers (`fxp_shard_sliver`,
untinted near-black) thrown off the creature's front at the contact and the hit cue's variant with a brittle crack
layer (PRESS's crack is a defence BREAKING; this is a guard being passed: the crack is shorter and drier).

**ONE SHAPE, ONE MEANING (the corpse-sourced pictures).** Four things leave or reach a corpse and each has its own
shape: CARRY is an instant line (above); WEEP's transfer is the only SLOW WET STRAND that travels (300 ms, 5.4);
HARDFACE's soften is a DARK GROUND RIPPLE, never a line (5.9); BACKDRAW's pull runs the OTHER way, corpse to bow
(5.14). BLOW BREAKTHROUGH + WEEP on one corpse: the line on the beat, the strand after it; two shapes, two meanings.

---

## 5. Per-item direction

Each block: PERCEIVE (what the player must read) / ANCHOR (the Core events and timing) / DRAW (shape, parts,
material, layer) / CLIP (strip and markers) / STATES (variations shown) / AUDIO / REMOVE / REUSE (closed references
on other champions) / NOTES.

Shared-skill recipes are keyed in the champion-agnostic tier `ByAnySkill[skillId]` (section 8) and resolve on any
champion; a signature is `BySkill[(champion, skillId)]`. The strip is always the champion's own; the look and the
cues belong to the skill.

### 5.1 hammer_blow - BLOW (T3 Signature weight, shared)
- PERCEIVE: one enormous blow every 6 beats; the body goes to the creature and comes back; under FINISH a SECOND,
  separate kill on the weakest creature; under BREAKTHROUGH the excess jumping on; under TRAIL the next swing armed.
- ANCHOR: Skill(slot) at the beat; the contact frame = the beat (`MeleeActionRecipe`, TravelMs 0); Strike(Primary)
  on the front creature; FINISH's extra Strike + EnemyDown at the same ms on the executed slot; Carry Strikes same ms.
- DRAW: HARD HANDS' root motion with a heavier profile (LungeBack -24 px, commit 110 ms accelerating, overshoot
  +10 px, the contact frame held TWO display frames (33 ms rigid hold), follow-through planted 200 ms, retreat
  280 ms). Impact = WEIGHT, not a slash: a flat shock ring on the ground under the contact (`fxp_ring_soft` squashed
  1.0 x 0.35, Source light, 0.5 -> 1.1 of the creature's width over 120 ms), a dust puff (`fxp_dust_soft`, untinted,
  alpha-blend, 0.4 of its height, 300 ms), a compressed white flash at the contact point (`fxp_flash_soft` 0.25 of
  its height, 2 frames), the body buckles x0.84 / x1.08 for 90 ms, TargetFlash 0.50 / 120. No sparks, no slivers
  (that is HARD HANDS' chip language) except FLATTEN's guard-break slivers.
- CLIP: the champion's `strike` strip + a new `.clip.json` (anticipation 0, commit 2, contact 4, recovery 6, settle
  7; StrikeHand socket on 3-5). On the Seeker that strip is the knife swing: the weight is carried by the impact,
  the hold and the cue, not by a new pose (no new animation in this sweep).
- STATES: FLATTEN -> guard-break accent + the crack-layer cue variant. TOLL -> nothing extra (the number). BREAKTHROUGH /
  CLEAN CUT -> CARRY (the instant line). TRAIL -> ARMED hand until the next swing's release, whose contact gets the
  guard-break accent. FINISH / BRINK / TWICE -> EXECUTE on the executed slot (another creature or the same): the
  blow's impact and the execute's cut-flash are two pictures on one frame; the execute's cue leads and the blow's hit
  cue takes the duck.
- AUDIO: `sfx_blow_commit` 0.30 (a heavy cloth whip + one boot plant), `sfx_blow_hit` 0.52 (a dropped sandbag on
  timber + a mallet on a plank, a low floor thud, no crack, ~140 ms), `sfx_blow_hit_flatten` (the same + a short dry
  crack), the execute `sfx_execute` 0.56 lead (section 4). Duck 0.45 from the commit to +160. BLOW's hit is one of
  the identity cues built per item (section 7); until it exists the archetype `sfx_fist_hit` plays.
- REMOVE: `fx_seeker_strike` / `fx_<champ>_strike` burst on the creature, `sfx_cast`, `sfx_hit` 0.22, the puff, the
  HAMMER callout; the 350 px swing in place.
- REUSE: none (BLOW is not HARD HANDS; pinned by `a_signature_action_belongs_to_its_skill` and a new
  `blow_is_its_own_recipe_on_every_champion`).
- NOTES: the replay must not pre-kill the executed creature before the contact frame: contact is the beat, so nothing
  is deferred.

### 5.2 snare_repay - REPAY (T2 Skill, shared)
- PERCEIVE: he is being hurt and KEEPS it; on the beat it snaps from him onto the one who bit him, as big as what he
  kept; BANKED: he turns it into a plate instead.
- ANCHOR: `Banked` (C1) at each bite; Skill(slot) at the beat; a `GestureRecipe` on the `cast` strip (commit marker
  on the beat; no contact of its own); Strike(Primary, Amount = payback) on the front creature at the beat; BANKED:
  ShieldGained at the beat (claimed); CARRIED: ShieldGained at ms 0 (silent).
- DRAW: THE BANK (ChampionStateLayer): 1-6 embers (`fxp_spark_dot`, Source light with a Body-red core) gathered at
  his guard shoulder (a share of his drawn body, like JAWS' belt anchor), count and size from the reported bank as a
  share of max health (1 ember >= 2 %, 6 embers >= 20 %), each new ember arriving from the bite's contact point on
  his body over 160 ms; quiet at rest (a +0.1 breath on REPAY's ready ping only); under VENGEANCE / RAW NERVE they
  dim over the def's window (cosmetic; the next report corrects them). THE DISCHARGE (not a projectile: nothing is
  in the hand, nothing is thrown): at beat -100 ms the embers snap from the shoulder to the front creature's body
  point as a REVERSED `ReturnStream` strand (3-6 motes by the bank on a tight arc, Source light over a hot core,
  100 ms), arriving ON THE BEAT; the `cast` strip plays in place with its commit on the beat (the gesture is the
  hurl of the arm, the strand is what leaves). Contact on the beat: a Source flash 0.2-0.4 of the creature's height
  by the bank, a ring, TargetFlash 0.38 / 130, the number at skill grade. A zero bank: the clip plays, no strand; a
  small grey fizzle puff at the shoulder (`fxp_dust_soft` 0.1), the 0 Strike draws no flash, no number, no cue.
  BANKED: the embers do not leave: the mass sinks into his chest and the barrier flares (the SHIELD claim);
  STANDING / LINING only change size. CARRIED: the barrier is up at ms 0. SCARRED: below 40 % health the embers burn
  brighter (+0.2) and the strand gains a red core.
- CLIP: the champion's `cast` strip + `.clip.json` (anticipation 0-1, commit 3, recovery 6, settle 7). The `trap`
  crouch is wrong for a payback and is never used by REPAY again.
- STATES: VENGEANCE / RAW NERVE / GRUDGE -> ember count and the cosmetic fade; SCARRED -> red; BANKED + STANDING +
  CARRIED; the ZERO-BANK fizzle (filmed: it is the state the player will see most on a tank build).
- AUDIO: `sfx_repay_release` 0.26 (a short air draw, a cloth snap) at the commit, `sfx_repay_hit` at 0.30 + 0.18 x
  clamp(bank / (0.25 x max health)) (0.30-0.48; a hot, airy impact: a canvas slap + a padded hit + a brief ember
  hiss), a zero cast: `sfx_repay_fizzle` 0.16 (a dry puff); BANKED: `sfx_repay_bank` 0.36 (a soft iron plate set +
  the air draw). Duck 0.45 from the commit to +160 only when the bank > 0.
- REMOVE: the row-wide rope ring (`fx_<champ>_trap`), `sfx_cast`, `sfx_hit`, the flash / thud / puff on a 0 hit, the
  SNARE callout (now REPAY), the rope before a shield.
- REUSE: none; the JAWS isolation pin (`the_jaws_recipe_belongs_to_the_skill_and_repay_keeps_its_own`) is updated to
  say REPAY has its OWN recipe.

### 5.3 sign_call - CALL (T2 Skill, shared)
- PERCEIVE: he calls, and from then on every hit of his lands bigger, for a while; SPEND: for three hits exactly;
  STEADY: for the wave, in steps.
- ANCHOR: Marked + Skill at the beat (the open); `AmpWindow` (C3) on every change (the close ms carried); `Spent`
  (C3b) per charge; amplified hits = Primary / Swing Strikes inside [open, until); the timeout is ended by the screen
  at the reported close ms (no event exists for it).
- DRAW: the SELF-EMPOWERMENT convention. The open: a `GestureRecipe` on the `mark` strip; at its commit marker a
  Source light flares at the pointing hand (`fxp_glint_star` 0.2 of his height, 120 ms) and settles behind his upper
  body as the standing glow. No reticle, nothing on any creature. The first amplified hit after the open takes the
  ring accent; every amplified hit takes the outlined number. SPEND: motes, each spend a ring; OVERSPEND brighter
  motes; COUNT five. STEADY: levels; REDOUBLE / PILLAR only change the level's brightness; FOUNDATION starts lit.
- CLIP: the champion's `mark` strip + `.clip.json` (commit 3 = the point; `PointHand` socket on frames 3-5).
- STATES: base expiry at +6 s (the glow goes out at the reported ms); SPEND's last charge closing the window (the
  last mote leaves and the glow is already gone); STEADY stacking across two casts.
- AUDIO: `sfx_call_open` 0.36 (a short resonant tap: a damped wooden tongue drum + a soft cloth whip; no bell), the
  accent silent, the close silent.
- REMOVE: `fx_<champ>_mark` reticle, `sfx_cast`, the SIGN callout (now CALL), the inspector's "FOR 600s" (reads the C3
  close) and "YOUR SKILLS" (now "YOUR HITS").
- NOTES: BRAND's per-wave builder must stop matching `Marked` by ms alone: it reads the Marked whose preceding event is
  its own Aura (same slot), so a CALL on BRAND's tick cannot be misread. Pinned.

### 5.4 volley_weep - WEEP (T0 affliction + a T2 transfer, shared)
- PERCEIVE: a death leaves bleeding behind; the bleeding sits on the next creature in front and keeps hurting it; a
  bigger pool bleeds harder; TORRENT fast and short, CARRION slow and long.
- ANCHOR: EnemyDown (the shown fall: a JAWS kill falls on the snap) + `Pooled` (C2) at the same ms = the transfer;
  Bleed Strikes every 500 ms = the ticks; `Pooled` with provenance = which skill feeds it.
- DRAW: THE TRANSFER (reaction layer, 300 ms; the one corpse-sourced picture that travels slowly): a strand of 4-6
  puffs (`fxp_trail_soft` + `fxp_spark_dot`, the feeding slot's Source light over a dark core) leaves the falling
  body's point and reaches the new front's body point; FLOOD doubles the strand when the pool was already > 0; ONSET:
  a thin strand from a cast's contact point into the front (120 ms). THE POOL (AfflictionLayer): 2-3 slow drips
  (`fxp_drip_soft`, Source light over dark) from the body point running 0.25 of the height over 800 ms, the rate from
  the pool (one drip per 1.6 s thin, three per 0.8 s at >= 30 % of the creature's max health), and a dark stain disc
  at its feet (`fxp_flash_soft` near-black, alpha-blend) growing with the pool. TICKS: no flash, no puff, no cue; the
  number at quiet grade. When the front falls the pool migrates with the next transfer (the layer re-seats by the
  shown fall). VENOM feeds the same pool: its provenance colours the drips Nature.
- CLIP: none. Reactions never take the figure; the `trap` clip is no longer loaded for any reaction (ActorClips).
- STATES: TORRENT + SPILLWAY + FLOOD (fast drips, a fast-fading stain, doubled strands) vs CARRION + DREGS + ONSET
  (slow thick drips, a persistent stain, cast feeds); a bleed-caused kill (the chain: a transfer from a creature that
  fell to a tick).
- AUDIO: the transfer's arrival `sfx_weep_arrive` 0.18 (one wet drop on stone), ticks silent, the stain silent.
- REMOVE: `fx_weep` and the CastRain profile (dormant), the `trap` clip load, `sfx_hit` 0.22 per tick, the flash and
  puff per tick (the generic path: Bleed / Reflect / Carry / Deadweight Strikes never flash, puff or thud: section 8).
- NOTES: the rail's fake rearm: WEEP has no ReactionArmed; the rail shows a passive (no cooldown sweep).

### 5.5 field_pulse - PULSE (T2 Skill, shared; new kind SHOCK, ground-plane)
- PERCEIVE: one stomp, and every creature is hit at once by something that came out of the ground under him;
  THRONG: bigger with the crowd; SHARE: equal pieces on exactly the struck ones.
- ANCHOR: Skill at the beat; a `GestureRecipe` on the `cast` strip (commit = the stomp, on the beat - 160 ms); the
  ShockPerformance's ring starts at the stomp and its rim reaches the FARTHEST struck creature on the beat; one
  Strike per creature at the beat (Core is instant: every contact is on the beat, whatever the ring's radius there).
- DRAW: a RADIAL GROUND SHOCK, not a front: a flat ellipse RING on the ground plane (`fxp_ring_soft` squashed to the
  stage's ground ellipse, Source light, 0.08 of a creature's height tall at most, alpha 0.5 -> 0.2) expands from his
  feet over 160 ms until its rim passes the farthest struck creature on the beat; it has no height, no crescent, no
  level travel. At each struck creature ON THE BEAT: an upward flick (a thin vertical Source flash 0.4 of its height,
  2 frames), a small buckle x0.92 / x1.04 (60 ms), TargetFlash 0.32 / 120, a skill-grade number. NARROWED / BALANCE:
  the ring is drawn whole (it is the ground) but only the struck creatures flick; the others take nothing. THRONG:
  the ring's alpha and the flick's height scale 1 + 0.12 per struck creature (cap 1.6). SHARE: nothing extra (equal
  numbers printed together say it).
- CLIP: the champion's `cast` strip (no `aura` strip exists for anyone; the fallback is now the rule) + `.clip.json`
  (commit 2 = the stomp, recovery 6).
- STATES: CROWDED on a >= 4 wave; SHARE + NARROWED (two creatures lit); BALANCE collapsing to one; a cast on the same
  ms as a MIRE tick (the last-owner rule, section 3: both draw, each its own).
- AUDIO: `sfx_pulse_release` 0.24 (one boot stomp + an air push, no tone), `sfx_pulse_hit` 0.40 ONE cue (a broad
  soft impact: a drum skin + a sandbag) + <= 2 ticks 0.16 at the outermost creatures. Duck 0.45 stomp -> +160.
- REMOVE: `fx_aura` on the champion, `sfx_cast`, N x `sfx_hit`, N flashes at full, the FIELD callout (now PULSE).

### 5.6 field_mire - MIRE (T2 field + T0 affliction, shared)
- PERCEIVE: the pack is standing in a mire; it bites more slowly because of it; the mire deepens (NUMB) or thickens
  with the crowd (TEEMING); corpses stay in it (REMNANT).
- ANCHOR: Aura(slot) per tick (1000 ms; 600 with RADIANCE), Slowed(amount) at the same ms = the depth, Strikes per
  creature = the damage, Heal (Nature) at the same ms.
- DRAW: a SEDIMENT BAND under the enemy row (GroundUnder layer): untinted umber-olive material with a soft top edge
  and a thin Source-light rim (alpha 0.5 material, 0.25 light), as wide as the row; its height = depth: 10 px at
  25 % slow to 30 px at 85 % (UI 150 %), darker as it deepens. Each tick: a slow ripple across the band (300 ms, left
  to right) and, on a tick that DEEPENED, the band rises 2 frames and settles. The creatures are SUNK by OCCLUSION,
  never by displacement (ADR-006: the actor box is where it is): the band's top strip is drawn OVER each creature's
  feet after the creature (one sprite per creature in the creature loop), its height = depth (4-14 px), so the feet
  disappear into the mire. THE DRAG (the cheapest honest form): on a creature's bite lunge the band's top strip
  under it stretches toward the hunter for 2 frames and snaps back (the mud holds its feet). TEEMING: thickness also
  grows with the living count; REMNANT: a dead creature leaves a low mound in the band at its place for the wave. The
  tick's damage: ONE summed number at quiet grade at the band's centre. The Nature heal: claimed (a faint green rise
  in the band, the chest glow, "+N" summed every 1.2 s).
- CLIP: none (a field).
- STATES: NUMB + DEEPEN (the band deepening tick by tick to its ceiling, then holding); TEEMING + CLOG (thick with 5
  alive); TEEMING + REMNANT with kills (mounds); RADIANCE's 600 ms clock; beside PRESS and beside WILT (both draw).
- AUDIO: ticks SILENT; `sfx_mire_deepen` 0.18 on a tick that raised the depth (a wet sediment settle: mud + gravel
  shift), none at the ceiling.
- REMOVE: `fx_aura` behind the champion, the wall-clock pulse, the single number over the front creature, the heal
  column per tick.

### 5.7 drain_drink - DRINK (T2 Skill, shared)
- PERCEIVE: he reaches for the front creature and life comes back to him in proportion; when the wave's ceiling is
  spent, the reach lands and nothing comes back; GLUT: a fed, swollen blow instead, bigger the healthier he is.
- ANCHOR: Skill at the beat; a MeleeActionRecipe in REACH mode on the `transformation` strip (the fist-forward frame
  is the contact; root motion 0: the `transformation` strips are self-buff flourishes on every champion, and a
  flourish carried to the creature reads as a power-up that slid); Strike(Primary) on the front; Heal(0, landed) at
  the same ms (if any) -> RETURN arriving +280 ms.
- DRAW: the body stays home. At the commit the fist-forward frame is a REACH: a pale pull-streak (`fxp_trail_soft`,
  2 frames) from the creature's body point to his hand bridges the gap (the Oathbound's tether convention: reach
  without moving the body). Contact on the beat = a GRAB: a Source ring that CONTRACTS into the contact point
  (`fxp_ring_soft` 1.0 -> 0.2 of the creature's height over 120 ms), TargetFlash 0.38 / 130; then the RETURN stream
  (motes by the healed share) along the same line to his chest, "+N" and the chest glow at the arrival. No Heal: the
  ring contracts, no stream, the number plain. GLUT / SURFEIT / STOUT: no grab; an outward impact ring + flash scaled
  by his health share (0.5 -> 1.0 of the creature's height), TargetFlash 0.45 / 120; RIPE above 90 %: a white rim.
  SIPHON / PUMP: the stream doubled. TRICKLE: every swing Heal gets the generic receive at quiet grade (1 mote, no
  text). A Nature signature's second Heal at the same ms joins the same stream (one arrival, one "+N" summed).
- CLIP: the champion's `transformation` strip + `.clip.json` (commit 2, contact 4, recovery 6; StrikeHand 3-5).
- STATES: a hurt hunter (visible return), the ceiling spent (no return), SIPHON + TRICKLE (swing sips), GLUT + RIPE at
  full health.
- AUDIO: `sfx_drink_commit` 0.28 (cloth), `sfx_drink_hit` 0.44 (a damp cloth squeeze + a short low pull: a wet grab,
  no cut), `sfx_drink_intake` 0.22 at the arrival (a bellows / tube air draw, no voice); GLUT: `sfx_drink_hit_glut`
  0.48 (a padded heavy impact). Duck 0.45 commit -> +160.
- REMOVE: `fx_<champ>_transformation` spiral on the champion, `sfx_cast`, `sfx_hit`, the puff, the heal column and the
  immediate "+N", the DRAIN callout (now DRINK).

### 5.8 drain_wilt - WILT (T0 affliction with a T2 cadence, shared)
- PERCEIVE: the creatures wither and bite softer; it deepens tick by tick to a floor; SHRIVEL: one body withers hard
  (two with HOLLOW) and the wither follows the front; SUP: a little life seeps back each tick.
- ANCHOR: Aura per tick; AttackBreak(slot -1 | 0 | 1, amount) = depth and subject at the same ms; Heal (SUP).
- DRAW: AfflictionLayer WITHER: the creature's draw tint lerps toward a dull grey-olive (luminance 1.0 -> 0.72, chroma
  pulled 40 %) by depth (0..-50 % maps 0..1; SHRIVEL to -90 / -97 % keeps going to 1.0 plus a DROOP: scale y 1.0 ->
  0.90, x -> 1.04, feet on the floor; a draw-time squash, the actor box unchanged). Each tick that deepened: 2-3 dry
  flakes (`fxp_shard_sliver`, untinted pale, alpha-blend) drift down from the body point over 600 ms and the body
  droops one step; at the floor: nothing (holds). Subject by AttackBreak slot: the whole row, or the front (and the
  second at half) mapped to the creature standing at that ms; a new front inherits by position (the layer re-seats on
  the shown fall). The biter's lunge flash is dimmed by its depth (BitePresentation reads the layer). SUP: a quiet
  RETURN each tick with a Heal (1-2 motes from the nearest withered body, no "+N" per tick: summed every 1.2 s).
- CLIP: none.
- STATES: SUP + BALM on a hurt hunter; SHRIVEL + HOLLOW with a front kill (migration); SHRIVEL + GAUNT on a boss (the
  deepest droop); beside PRESS and MIRE.
- AUDIO: `sfx_wilt_deepen` 0.14 per deepening tick (dry leaves + a small paper crumple), silent at the floor; SUP's
  intake silent (quiet grade).
- REMOVE: `fx_wilt` held on the hunter (the file is repaired for the library, not played in combat), the wall-clock
  pulse, the heal column and per-tick "+N".

### 5.9 sig_anvil_hardface - HARDFACE (T3 Signature, anvil)
- PERCEIVE: a hammer-weight blow on the beat; then, whenever something dies, every survivor visibly SOFTENS (cracks),
  deeper with each death, until it cannot soften more; PEENING: the blow itself cracks its target.
- ANCHOR: Skill + Strike at the beat (contact = beat). THE SOFTEN reads Break events ONLY in two batches: a Break
  that FOLLOWS an EnemyDown in the same ms batch (the kill strip, `SoloBattle.cs:1841`), and a Break in a batch that
  holds a HARDFACE Skill (PEENING, `:2847`). A Break at an Aura ms is NOT HARDFACE's: PRESS emits Break / DefenceNow
  on every tick (`:2358`), and an Anvil weaving PRESS must not spread a corpse-shock from no corpse every 2 s.
- DRAW: THE BLOW: MeleePerformance on `anvil_strike` with the heaviest profile in the game (commit 130 ms, overshoot
  +14, contact held 2 frames, follow-through planted 230 ms, retreat 300 ms); impact = weight (flat ring, dust,
  compressed flash, buckle x0.84 / x1.08) in the slot's Source light, TargetFlash 0.50 / 120. THE SOFTEN: at a kill
  with Break events, a dull DARK GROUND RIPPLE (`fxp_ring_soft` squashed to the ground ellipse, near-black,
  alpha-blend, not light; never a line) spreads from the corpse's feet under the survivors over 160 ms, and each
  survivor takes a CRACK (AfflictionLayer): 1 / 2 / 3 short dark jagged strokes (`fxp_crack_soft`) seated above the
  body point by stack tier (<= 1 / <= 3 / more), persistent; at the floor the cracks hold and new kills spread no
  ripple. PEENING: the crack on the front at the contact, no ripple. UPSET: the Anvil lunges to the front and slams
  the GROUND: a DUST SKIRT (`fxp_dust_soft` x 4-6, untinted, dark, flat, alpha-blend) spreads along the ground from
  the slam under the whole row over 120 ms (no crescent, no front, no light), and every creature contacts on the beat
  with 2 / 3 pulses 70 ms apart (ContactTicks; the hits are one ms in Core, the pulses are accents), numbers summed
  "-N x2".
- CLIP: `anvil_strike` + `.clip.json` (commit 2, contact 4, recovery 6; StrikeHand 3-5).
- STATES: a cast; the first kill's spread; 2+ stacks; the floor; PLANISH + PEENING (a crack without a kill); UPSET +
  THIRD DROP on 4 creatures; the Anvil beside PRESS (PRESS's ticks spread nothing).
- AUDIO: `sfx_hardface_commit` 0.32 (a heavy boot plant + cloth), `sfx_hardface_hit` 0.52 (a padded heavy fist +
  a damped anvil thud, no ring), `sfx_hardface_soften` 0.22 ONCE per kill (a brittle plaster / clay crack; 0.22 keeps
  it under the affliction ceiling 0.238), never per survivor; UPSET: the hit cue + 2 quiet ground ticks 0.18.
- REMOVE: `fx_anvil_strike` crescent, `sfx_cast`, `sfx_hit`, the puff, the HAMMER callout (now HARDFACE). The arena
  badge `xN` (`DrawBreakBadge`, reconstructed from `CreatureBreaks`) STAYS: it is the only arena read of PRESS's and
  PEENING's breaks on builds without HARDFACE's layer, and it is cheap.
- NOTES: the strip fires on kills from any source; the spread is keyed to the Break events in a kill batch, never to
  the cast and never to an Aura batch.

### 5.10 sig_chorus_grave_song - GRAVE SONG (T2 field, chorus)
- PERCEIVE: the song is EVERYWHERE and nothing travels: every two seconds it reaches every creature at once; the
  dead sing it (the more are dead, the louder it is); CHANTRY: the wave itself is hushed and bites softer per dead.
- ANCHOR: Aura per tick (2 / 3 / 4 s), Strikes per creature same ms; EnemyDown count this wave = intensity; the
  place where each dead creature fell (its slot box) = where a hum stands.
- DRAW: THE HUMS: at the feet of every dead creature's place a faint RISING HUM: one sprite each (`fxp_flash_soft`
  stretched 0.15 x 0.5 of a creature's height, Source light, alpha 0.12 at rest), drifting up 6 px and back over
  1.6 s, seated when the fall is shown; 0 dead = no hums; the Chorus's charms carry one hum at her hand socket
  (alpha 0.10, the song's source) so a wave with no dead still shows the song is on. THE TICK: a VERTICAL SHIMMER
  rises through the whole row at once (`fxp_flash_soft` stretched to the row's width x 0.08 of a creature's height,
  Source light, from the ground to 1.1 of their height over 180 ms, alpha 0.25 + 0.06 per dead, cap 0.55), the hums
  flare x2 for 2 frames, and every LIVING creature shivers (x +-0.03 horizontal, 2 frames) with a soft flash 0.25 /
  110 and one number each at quiet grade. Nothing leaves the Chorus, nothing arrives from anywhere: the shimmer is
  the row's own air. REQUIEM / DIRGE: the shimmer slower (300 ms) and deeper in colour, the hums taller. CHANTRY: no
  growth; survivors carry a HUSH (AfflictionLayer tint: cooler, 10 % darker per dead to 25 %) and their lunge flash
  dims; HUSH / BLACK VEIL only change the cap.
- CLIP: none.
- STATES: tick at 0 dead, after 2 deaths (two hums), late (four hums); REQUIEM + DIRGE; CHANTRY + HUSH with an
  EnemyStrike after deaths; a tick sharing a ms with a cast (x0.6).
- AUDIO: the TICK is the RATTLE: `sfx_grave_song_tick` 0.26 -> 0.32 by dead count (a bone / wood chime rattle, no
  voice; CHANTRY: 0.24); the TOLL (`sfx_grave_song_toll` 0.34, a damped large bell, 400 ms decay allowed) plays ONLY
  on the first tick after the dead count ROSE (the "louder" moment, once per death), never on every tick; REQUIEM:
  the toll heavier. Never lead, x0.6 in a focus window. This is the one bell in the game (section 7).
- REMOVE: the held `fx_aura`, the one number over the front, the wall-clock pulse, `fxp_front_soft` (never made).

### 5.11 sig_metronome_clockwork - CLOCKWORK (T3 Signature, metronome)
- PERCEIVE: a clock nobody can hurry runs down; on the next beat after it, bolts strike three creatures at once;
  HELD NOTE: one chime falls on all of them.
- ANCHOR: the Skill event is in the replay ahead of time: the TELEGRAPH is the 600 ms before it; release = beat -
  250 ms from the `projectile` strip's release marker; one Strike per hit per target on the beat.
- DRAW: TELEGRAPH (ChampionStateLayer): a small Source pendulum glint swings at his chest (3 swings, 200 ms period)
  with three quiet ticks. THE VOLLEY: ProjectileActionRecipe: one escapement BOLT (the Metronome's missile: a brass
  dart with a gear-tipped head, `prop_metronome_bolt` + `_edge`) per struck creature, SPRAY's fan and flight, a
  Source-light trail and glint, a directional contact (a sharp short slash + 3 slivers), TargetFlash 0.45 / 120. ROLL /
  RIM SHOT: two (three) bolts per target released 40 ms apart, the later ones landing as contact ticks (+36 ms accents;
  the number is one). HELD NOTE: no bolts: a DROP of one wide thin bar of light (`fxp_flash_soft` stretched over the
  row, Source) from above, landing on every creature on the beat with a small buckle and one chime; SPARE SHOT's
  doubled hits are the "x2" number.
- CLIP: `metronome_projectile` + `.clip.json` (anticipation 1, commit 2, release 3, recovery 6; ThrowHand 1-3).
- STATES: the last second of the clock; release; contact on 3; ROLL + RIM SHOT + OFF BEAT (the fan already names the
  real targets; nothing to redirect; a kill mid-volley filmed); HELD NOTE + SPARE SHOT on 2 creatures; FIRST BEAT's
  opening cast at wave start.
- AUDIO: `sfx_clockwork_tick` 0.14 x3 at -600 / -400 / -200 (a small mechanical escapement tick), `sfx_clockwork_release`
  0.30 (a latch + a spring), `sfx_clockwork_hit` 0.46 (a small hammer on brass, damped) + ticks 0.18; HELD NOTE:
  `sfx_clockwork_chime` 0.46 (a struck TUNING FORK / a brass bar, 300 ms decay: a note, not a bell; the bell is
  GRAVE SONG's). Duck 0.45.
- REMOVE: `fx_metronome_projectile` static strip, the single flight to one creature, `sfx_cast`, the VOLLEY callout
  (now CLOCKWORK).

### 5.12 sig_unbroken_hold_fast - HOLD FAST (T2 field on the hunter, unbroken)
- PERCEIVE: every two seconds a plate is set onto his wall; the wall stands and takes bites; a full wall is simply
  full (a quiet pass of light along it says the tick came); DEEP ROOTS: roots feed him with it.
- ANCHOR: Aura per tick; ShieldGained at the same ms (claimed); Heal at the same ms (DEEP ROOTS); GROUNDWORK's
  ShieldGained at ms 0.
- DRAW: THE PLATE (only on a tick WITH a ShieldGained): a chamfered stone slab (`fxp_plate_soft`, untinted grey-brown,
  alpha-blend, 0.35 of his height wide) drops into place in front of his chest from 30 px above over 90 ms, settles 2
  frames, and dissolves into the standing barrier (`fx_shield` brightens +0.2 for 150 ms); the bar rim flares
  (existing). AT THE CAP (an Aura with no ShieldGained): NO plate, nothing drops, nothing crumbles: one 150 ms RIM
  PASS of light along the barrier's edge (alpha +0.15), silent. On a tank build the cap is most of the fight, and a
  plate crumbling every 2 s forever would be T2 motion on the hunter at rest. BREASTWORK: a larger plate (0.5) every
  4 s. GROUNDWORK: the barrier is up at ms 0, no drop. DEEP ROOTS: at the heal, 2-3 roots (`fxp_trail_soft` curved,
  Nature / Source light) climb from the ground into the plate over 200 ms (RETURN from the ground), the chest glow,
  "+N" at the arrival; TAPROOT: more roots by pulse index (1 -> 4).
- CLIP: none.
- STATES: a tick below the cap, a tick at the cap (the rim pass), a bite absorbed, a break, BREASTWORK + GROUNDWORK,
  DEEP ROOTS + TAPROOT over four pulses.
- AUDIO: `sfx_hold_fast_set` 0.30 (a stone set on stone + a short steel latch, no ring); the cap silent; roots silent;
  the generic `sfx_shield_gain` is NOT played on HOLD FAST's ticks (claimed); absorb / break unchanged.
- REMOVE: the held `fx_unbroken_trap` spikes, the "+N SHIELD" callout on its ticks, the wall-clock pulse.

### 5.13 sig_tower_slow_fall - SLOW FALL (T2 field, tower; new kind DROP)
- PERCEIVE: a stone falls onto the front creature, on time, every few seconds; he never moves; COURSES: small quick
  stones; ONE STONE: one slab across two or three.
- ANCHOR: Aura per tick + the Strike(s) at the same ms name the targets; the DropPerformance is scheduled from the
  pre-read wave events (as FieldPerformance is) so the telegraph starts at -450 ms and the stone enters at -150.
- DRAW: the DROP convention with the Tower's stone (`fxp_stone_block`: a hand-drawn wedge, untinted, soft edges, from
  `fx_tower_strike`'s best frame as the source): shadow disc, the fall, contact on the tick: dust, a flat ring, a
  buckle x0.86 / x1.08, chips (`fxp_shard_sliver` untinted), TargetFlash 0.40 / 120, the number after contact (same
  frame), a kill falls on time; the stone fades over 250 ms. COURSES / DRYSTONE: half-size stones, the telegraph
  shortened to -300 ms (a 1 s clock cannot carry a 450 ms shadow), the flash 0.30; PLUMBLINE / HAIRLINE under the
  threshold: the white number outline; defence-ignore: the guard-break slivers. ONE STONE / CAPSTONE: one slab
  stretched across the 2-3 front creatures, one shadow spanning them, each buckles. Inside a focus window (DRYSTONE
  under every action): x0.6.
- CLIP: none. The Tower stands ("Slow. Arrives anyway.").
- STATES: telegraph, contact, a tick that kills (the front moves), COURSES + DRYSTONE, ONE STONE + CAPSTONE + FULL
  COURSE, a stone on a cast's beat (quiet).
- AUDIO: `sfx_slow_fall_hit` 0.40 ONE cue (a flagstone dropped on earth + a gravel scatter); COURSES / DRYSTONE
  `sfx_slow_fall_hit_small` 0.24 (a smaller stone; a cue at 1 Hz sits with the fast-tick ceiling, section 3); ONE
  STONE + ticks 0.16 per extra creature; the fall is SILENT.
- REMOVE: the held `fx_tower_strike` looping on the champion, the summed silent number, the wall-clock pulse.

### 5.14 sig_quiver_backdraw - BACKDRAW (T3 reaction, quiver)
- PERCEIVE: that death SENT arrows: the fall pulls the shot, and two arrows land on the survivors; CLEAN SWEEP: the
  whole row; ONE SHAFT: one heavy shaft.
- ANCHOR: the batch EnemyDown -> Skill(slot) -> Strike per arrow at the kill ms. The arrows' feedback is ECHOED to
  their arrival (ReactionEcho: numbers, flashes, the arrow kills' falls via `_deathDeferred`); a Skill with no Strike
  in its batch draws nothing (the phantom).
- DRAW: the BACKDRAW (80 ms; the one corpse-sourced picture that runs TOWARD the hunter): a thin Source streak from
  the falling creature's body point back to the Quiver's bow hand (a `BowHand` anchor as a share of his drawn body);
  the LOOSE: a 2-frame vertical bowstring flash at the hand; the ARROWS (the Quiver's missile `prop_quiver_arrow` +
  `_edge`, composed: head, a short Source trail, a glint): one per struck creature, 180 ms flight, SPRAY's fan;
  arrival: a thin forward slash + a small flash 0.35 / 110, the number, the fall. The figure is never taken. ONE
  SHAFT: one arrow at 1.6x, a heavier contact (0.5 / 120, 3 slivers), BROADHEAD: the WEEP pool layer shows the bleed.
  CLEAN SWEEP: the fan to every creature; SECOND STRING: two arrows per creature 36 ms apart. A second trigger inside
  a phrase joins it (no restart).
- CLIP: none (no `trap` clip; the figure keeps whatever it is doing).
- STATES: the first non-final kill (base), CLEAN SWEEP + SECOND STRING, ONE SHAFT + BROADHEAD, the wave-clearing kill
  (nothing drawn), a swing kill and a cast kill (overlapping performances).
- AUDIO: `sfx_backdraw_loose` 0.30 (a bowstring twang + an arrow whip), `sfx_backdraw_hit` 0.40 (an arrow into hide:
  a thump + a short wooden clack) + ticks 0.16; ducked 0.45 under an action; no callout.
- REMOVE: `fx_quiver_projectile` strip flight, `sfx_cast`, the pitched reaction thud, the VOLLEY callout, the trap clip,
  the phantom on the last kill.

### 5.15 sig_thornwall_narrows - NARROWS (T3 reaction, thornwall)
- PERCEIVE: it bit him, and the road narrowed on it: stakes closed in from both sides; each answer this wave is
  bigger than the last; BRAMBLE: on everyone; CHOKE: faster and sharper.
- ANCHOR: EnemyStrike -> Skill -> Strike(front) at the bite ms; the phrase snaps at +120 ms (the answer's number,
  a soft flash and a kill's fall echoed to the snap); the growth tier = the count of this slot's prior Skill events
  this wave (0 / 1-2 / 3+).
- DRAW: two rows of THORN STAKES (`fxp_thorn_stake`: a tapered spike, untinted dark wood with a Source-lit edge)
  rise from the ground on both sides of the front creature (2 + tier stakes a side, cap 6; height 0.45 -> 0.7 of
  its height by tier) and close inward over 120 ms; the SNAP at +120: the stakes meet its silhouette (the near row
  drawn in front, the far row behind: `DrawBehind` as JAWS), a soft flash 0.25 / 90, the number, the fall; hold 80 ms;
  sink 200 ms. BRAMBLE: stakes under every struck creature, smaller (0.35), no tier; UNDERGROWTH / BLACK THORN under
  the threshold: the white number outline. CHOKE / SECOND STAKE: two targets, the stakes rise faster (90 ms).
- CLIP: none; the Thornwall holds his guard.
- STATES: answer #1 and answer #4 of one wave, BRAMBLE on 4 creatures, CHOKE + SECOND STAKE, an answer that kills.
- AUDIO: `sfx_narrows_answer` 0.42 at the snap (a stake driven into soil + a dry bramble rustle), tier 2 / 3 add one
  and two layered stake hits and pitch -0.02 per tier; ducked 0.45 under an action; the bite's own thud stays.
- REMOVE: the row-wide barbed ring, `sfx_cast`, the pitched thud, the SNARE callout, the trap clip.

### 5.16 sig_oathbound_oathmark - OATHMARK (T2 reaction opening + T0 affliction, oathbound)
- PERCEIVE: he was hit, and his oath bound them: every covered creature wears a sworn band; his hits land bigger while
  it holds; it falls away when the window closes; SEALED WORD: one deep band on the front; OPEN WORD: light bands on
  all with a deeper one in front.
- ANCHOR: EnemyStrike -> Marked (percent) -> Skill at the bite ms; `AmpWindow` (C3) at every change (the close ms
  carried); amplified hits = Primary / Swing inside the window ON A BANDED creature (under SEALED WORD only the front
  is amplified and only the front wears a band: the accent follows the band, never the window alone).
- DRAW: the OPEN (reaction layer, 160 ms): from his raised hand a dark streak to each covered creature; the BAND
  (AfflictionLayer): two half-rings of dark forged iron (`fxp_oath_band`, untinted, drawn once mirrored) clasp around
  the body point at 0.5 of the body width, meeting in 100 ms with a Source seam glint at the join; at rest it rides the
  body with the seam stepping every 800 ms. Depth: SEALED / DEEP SEAL = a thicker band + a second band; FIRST WORD =
  the front's band thicker than the rest; SAID AGAIN = thickness by reopen count (filmed at the cap). Amplified hits
  on a banded creature: the outlined number and the flash rim; the ring accent on the FIRST hit after each open only.
  THE CLOSE: the bands open and fall away over 150 ms at the reported close ms (a timeout) or the count-close event.
  Reopening re-clasps without removing.
- CLIP: none.
- STATES: the opening, mid-window with hits, the close, SEALED WORD vs OPEN WORD + FIRST WORD, SAID AGAIN at cap.
- AUDIO: `sfx_oathmark_open` 0.34 (an iron clasp shutting + a short chain shiver; NO thud), the accent silent, the
  close silent.
- REMOVE: the chain ring over one creature, `sfx_cast`, the damage thud, the SIGN callout, the trap clip.

### 5.17 sig_magpie_paying_work - PAYING WORK (T3 Signature on every wave, magpie)
- PERCEIVE: a quick heavy cut and she takes something back; on a boss, a bigger cut (she cuts twice); STRIPPED BARE:
  nothing comes back, more goes in; LIGHT FINGERS: a touch on everyone, more comes back.
- ANCHOR: Skill at the beat; contact = beat on the front; Heal at the same ms -> RETURN +280 ms; the boss read from
  presentation state (the wave's boss slot).
- DRAW: a FAST lunge (commit 80 ms, overshoot +16, a quick back-step retreat 180 ms); contact = a CUT: a bright
  directional slash along the incoming line (SPRAY's contact grammar, Source light), 3 slivers, TargetFlash 0.45 / 120;
  the LOOT: 3-5 glints (`fxp_glint_star`, Source light) burst from the creature and return to her hand, arriving +280 ms
  with "+N" and the chest glow. BOSS: still T3 (`BossPower` fires on EVERY cast of a boss wave, every 5 beats; a
  cadence is never Major): a BIGGER cut, two crossing slashes, impact 0.8 of the height, flash 0.50 / 120, hit cue
  0.52, NO white-hot frame, the cue does not lead. STRIPPED BARE / PRISED OPEN: no loot; the guard-break slivers;
  CLEANED OUT: the second cut on any target. LIGHT FINGERS: the lunge to the front only; every other struck creature
  takes a glint-touch (a small glint pop at its body point on the beat, flash 0.25); FULL HANDS: more glints return.
- CLIP: `magpie_strike` + `.clip.json` (commit 2, contact 4, recovery 5-6: a short clip; StrikeHand 3-5). If the
  strike strip's contact pose reads weaker than `magpie_attack`'s dagger slash on the contact sheet, the attack strip
  (cleaned of its baked arc) is the fallback.
- STATES: an ordinary wave, a boss wave, STRIPPED BARE, LIGHT FINGERS on 4-5 creatures; the hunter hurt.
- AUDIO: `sfx_paying_work_commit` 0.28 (a quick cloth whip), `sfx_paying_work_hit` 0.52 (a knife into leather + a short
  coin-purse snatch jingle; the boss cut the same cue with its second slash as a +40 ms tick 0.20),
  `sfx_paying_work_take` 0.22 at the arrival (coins settling, tiny).
- REMOVE: the gem shower on the champion, the self pose, `sfx_cast`, `sfx_hit`, the heal column, the DRAIN callout
  (now PAYING WORK).

### 5.18-5.27 form:* - the ten basic attacks (T1 Ordinary)
One table; the shared rules are in cohort A. Every basic attack is resolved by a new `SwingRecipes.For(champion)`
tier (the basic attack is not a SkillDef) and performed only when `char_<id>_attack` has its `.clip.json`. A melee
basic's STEP-IN is a draw offset curve over the clip (0 at the commit frame, 20-25 % of the row gap at the contact
frame, 0 by the settle frame); it never uses the MeleePerformance root-motion / retreat machinery, so there is no
"never home at TEMPO 60" regime to measure away.

| Champion | Verb | Kind / motion | Contact picture (Source light on the edge only) | Hit cue (0.36; one cue) | Strip work |
|---|---|---|---|---|---|
| seeker | a step-in blade cut | MELEE, 25 % step-in | a thin directional slash 0.3 of the creature's height + 2 slivers | `sfx_seeker_swing_hit` -> `sfx_blade_hit` | `.clip.json` (contact 4, StrikeHand 3-5) |
| anvil | a lunge punch | MELEE, 20 % step-in | a compressed flash + a small flat ring | `sfx_anvil_swing_hit` -> `sfx_fist_hit` | `.clip.json` |
| metronome | a running punch | MELEE, 25 % step-in | a compressed flash + 2 sparks; FIRST BEAT's first hit on each creature = the white number outline | `sfx_metronome_swing_hit` -> `sfx_fist_hit` | `.clip.json` |
| tower | an overhead hammer slam | MELEE, 20 % step-in (the slam lands toward the creature, not at his feet; the contact picture is AT the creature) | dust + a flat ring (0.35) | `sfx_tower_swing_hit` -> `sfx_stone_hit` | `.clip.json` |
| thornwall | a shield bash | MELEE, 20 % step-in | a broad short flash (0.35 wide, 2 frames) | `sfx_thornwall_swing_hit` -> `sfx_wood_hit` | `.clip.json` + BAKED FLASH removed from frames 3-5 |
| magpie | a dagger nick | MELEE, 25 % step-in, the fastest profile | a thin bright slash + 1 sliver | `sfx_magpie_swing_hit` -> `sfx_blade_hit` | `.clip.json` + the baked green arc removed (the slash is the recipe's smear) |
| quiver | a bow shot | PROJECTILE, no step, release = beat - 200, `prop_quiver_arrow` | an arrow contact: a short forward slash | `sfx_quiver_swing_hit` -> `sfx_blade_hit`; release `sfx_quiver_loose` 0.18 -> `sfx_throw_release` | `.clip.json` (release 4, BowHand 3-5) |
| chorus | a thrown bone charm | PROJECTILE, `prop_chorus_charm` (one charm; the bundle in the hand = the charms she holds) | a bone clatter: a small flash + 3 pale slivers | `sfx_chorus_swing_hit` -> `sfx_wood_hit`; release `sfx_chorus_toss` 0.16 -> `sfx_throw_release` | `.clip.json` + baked charms and glow removed from the late frames |
| unbroken | a flung stone chip (DECIDED: the hand gesture is a throw; a wall champion does not lunge) | PROJECTILE, `prop_unbroken_chip`, a short heavy lob (TravelMs 220) | a stone chip crack: a small flash + 2 untinted chips | `sfx_unbroken_swing_hit` -> `sfx_stone_hit`; release `sfx_unbroken_toss` 0.16 -> `sfx_throw_release` | `.clip.json` + the baked orange glow removed |
| oathbound | a chain lash | REACH: the chain body (`prop_seeker_chain_body` reused as a shared iron material; the Oathbound's hook `prop_oathbound_hook` at its end) stretches from the hand socket to the creature over the clip's lash frames, snaps taut on the beat, recoils 120 ms; no step | a spark + a small flash 0.30 at the hook | `sfx_oathbound_swing_hit` -> `sfx_fist_hit` (until its own cue) | `.clip.json` (contact 4, LashHand 2-5) |

Shared for all ten: TargetFlash 0.30 / 110, the number plain, no callout, no duck, the 40 px post-hit push and the
`fx_weakhit` puff removed, `sfx_hit` never played for a performed swing. DEADWEIGHT (anvil): the stored lump (5.31).
MOMENTUM (tower): nothing beyond the number. The yield / handoff rules of ADR-011 apply unchanged (a step-in clip
hands off like any plain clip). The arrow after each hit cue is the resolution chain: the archetype cue plays from
the first film (section 7), the per-champion cue replaces it when built.

### 5.28 proc:heal - the generic receive (T0)
The RETURN convention without a stream: a chest glow + "+N" summed over 400 ms, silent. Any recipe that owns the Heal
(DRINK, PAYING WORK, HOLD FAST's roots, WILT's SUP, MIRE's Nature heal, TRICKLE) suppresses it at that ms. `fx_heal`
is no longer spawned (the file moves to the legacy folder, section 6).

### 5.29 proc:shield - keep
Not presentation work beyond the CLAIM hook (section 4). The barrier, gain, absorb and break stay as they are; the
`RH_SHOT_SHIELDFX` poses still film them.

### 5.30 proc:undying - UNDYING (T4 Major)
- PERCEIVE: the killing bite lands and he refuses it.
- ANCHOR: the EnemyStrike that would have downed him, then Undying(0, ms) at the same ms.
- DRAW: his figure holds (no Down); a GOLD ring collapses INTO him (`fxp_ring_soft` 1.4 -> 0.3 of his height over
  180 ms, gold: the keystone's ink) with one white-hot frame at the end, the barrier flares gold for UndyingShieldMs,
  the callout UNDYING stays.
- AUDIO: `sfx_undying` 0.60 lead (one deep air intake + one heavy heartbeat thump + a low wooden drum hit; NO bell:
  the game's one bell is GRAVE SONG's toll).
- NOTES: fixture: the unbroken (SECOND WIND) or the keystone, `RH_SHOT_ENEMY` lethal bite.

### 5.31 proc:deadweight - DEADWEIGHT (T0, anvil)
- DRAW: on DeadweightStored a dull dark LUMP (`fxp_flash_soft` near-black, alpha-blend, 0.12 of the body height)
  sinks into the creature below its body point (120 ms) and stays; on DeadweightReleased + the Deadweight Strike it
  DROPS out of the body (falls 20 px and fades, 150 ms); the Strike's number at quiet grade captioned nothing.
- AUDIO: silent (the contact's own cue is the sound).

### 5.32 proc:weaver_echo - the ECHO mode (infrastructure)
Every performance class takes an `Echo` flag for the second Skill at the same ms: no champion clip (the figure is
busy with the first cast), the projectile / contact / shock layers at x0.6 size and brightness, cues x0.5 and no
release cue, ONE callout (the first), no second `sfx_cast` on legacy skills. A recipe skill woven under WEAVER keeps
its look at echo scale instead of falling to the plain path (the performance is keyed by the Skill event, not the
cast's slot).

### 5.33 proc:bleed_reflect - bleed ticks and the THORNS reflect (T0)
The generic path rule (section 8): HitSource Bleed / Reflect / Carry / Deadweight never flash, puff or thud; numbers
at quiet grade. The pool is drawn by WEEP's layer from `Pooled` with VENOM's provenance (Nature drips). Reflect: a
single Source spark (`fxp_spark_dot`, 2 frames) at the biter's contact point on the bite, silent.

### 5.34 proc:charge - CHARGE (HUD)
The HUD chip stays the truth. The REND dump: the Strike after a Charge drop gets the EMPOWERED-HIT accent (the ring,
once: a dump is a countable moment) in the keystone's Source. No arena storing picture (decided: the figure already
carries CALL / REPAY states). Filmed (segment 56).

### Closed references on other champions (the champion-agnostic tier)
- **SPRAY (`volley_spray`) elsewhere**: the SAME ProjectileActionRecipe timing, fan, flight, contact hierarchy, cue
  family and volumes (`sfx_seeker_spray_*` are the Seeker's; the agnostic tier resolves `sfx_<champ>_spray_*` ->
  `sfx_spray_*` (a shared thrown-object family built once) -> the archetype chain). The MISSILE is the champion's
  own (the one prop each champion gets, section 6); the clip is the champion's `projectile` strip with its
  `.clip.json`. The Seeker's knife stays the Seeker's. Not redesigned: a different object on the same contract.
- **JAWS, PRESS and BRAND elsewhere**: the closed recipes REUSED BYTE-IDENTICAL, with NO Source parameter and no
  colour-table refactor. The owner approved JAWS as "one Shadow phenomenon", PRESS as one picture (`fab25919`) and
  BRAND's corruption as Shadow by nature: each is the SKILL's identity, not the Seeker's, and the same reasoning
  applies to all three. The slot's Source shows in the NUMBER (its outline / caption colour), nowhere else. The
  cues are reused through the resolution chain (`sfx_seeker_jaws_bite` as `sfx_jaws_bite`, the PRESS tick, the seven
  BRAND cues): one file each, no re-approval question, no regression film needed for colour (only the resolution
  tier changes, pinned by the recipe-isolation tests).
- **HARD HANDS**: Seeker-only, no agnostic entry.
Resolution: `BySkill[(champ, skill)]` -> `ByAnySkill[skill]` -> `ByForm[(champ, clipKey)]` -> legacy. An agnostic
ACTION is performed only when the champion's strip for the recipe's ClipKey has a `.clip.json`; reactions, fields
and marks have no such requirement. Pinned by `agnostic_recipes_resolve_on_every_champion_test` (every (champion x
shared skill) pair resolves to the expected recipe and the five Seeker pairs are unchanged).

---

## 6. Asset strategy

### Legacy strips
NOTHING IS DELETED in this sweep. A deleted asset is a silent no-op on the legacy path, and the legacy path is the
design's own QA twin: `fx_seeker_projectile_strip8_512` is the KEY of SPRAY's legacy composite flight
(`ProjectileLook.cs:234` -> `VfxPlayer.cs:269`), so deleting it blanks SPRAY under `RH_ACTION_RECIPES=0`; `fx_aura`
is the held field of PULSE / MIRE / GRAVE SONG under `RH_FIELD_RECIPES=0`. Retired strips MOVE to
`assets/art/VFX/legacy/` (still loaded by key, excluded from `check_asset_consumers.py`'s reached-families gate and
from `check_fx_edges.py`), and are deleted in a follow-up commit after the owner accepts the sweep.

| Action | Strips | Why |
|---|---|---|
| RETIRE to `legacy/` (after `check_asset_consumers.py` shows no live-recipe consumer) | the 50 per-character `fx_<champ>_{mark,projectile,strike,transformation,trap}` incl. `fx_seeker_projectile` / `fx_seeker_transformation`; `fx_strike`, `fx_mark`, `fx_hit`, `fx_weakhit`; `fx_projectile`, `fx_trap`, `fx_transformation`; `fx_weep`; `fx_heal`; `fx_aura` | one-bit stamps or dormant; every live consumer is replaced by a composite; the twin still finds them |
| REPAIR (file fix, library only) | `fx_wilt` (set RGB = 255 where A > 0: the double-premultiplication) | cheap, deterministic; no combat consumer after 5.8 |
| REPAIR (soften pass, `fxclips.py` feather) and KEEP | `fx_death`, `fx_shield_break`, `fx_crit` | still played (falls, breaks, crits); fail SOFT only |
| KEEP untouched | `fx_shield`, `fx_press`, `fx_seeker_mark`, `fx_seeker_strike`, `fx_seeker_trap` (the Seeker's Form art for `RH_ACTION_RECIPES=0` films), the PRESS and JAWS parts, the props | accepted / soft |
| CLEAN (strip baked VFX; `strip_bake.py`: chroma-key the baked colour per strip, review on a contact sheet) | `char_thornwall_attack` (white flash, frames 3-5), `char_magpie_attack` (green arc), `char_chorus_attack` (charms + blue glow, late frames), `char_unbroken_attack` (orange glow + spark) | the recipe draws the effect; a baked one fights the Source light |
| AUTHOR timing NOW (`.clip.json`, no new drawing): the ~22 files the film needs | Seeker: `attack`, `strike` (BLOW), `cast` (REPAY, PULSE), `mark` (CALL), `transformation` (DRINK); `attack` x9 (the other champions); `metronome_projectile` (CLOCKWORK); `quiver_projectile` (SPRAY-agnostic); `anvil_strike` (HARDFACE); `magpie_strike` (PAYING WORK); `chorus_cast` (PULSE on the Chorus, segment C); `unbroken_cast` (REPAY on the Unbroken, segment F) | markers + sockets proposed by a new `clip_markers.py` (frame-delta energy peaks -> contact / release candidates, hand positions from the brightest moving blob), reviewed on contact sheets; ~1 min a file after the tool exists |
| AUTHOR timing LATER (a tail phase, section 8) | the remaining ~38 (`cast`, `strike`, `projectile`, `mark`, `transformation` on the champions the film does not pose them on) | the agnostic tier already falls to legacy without a `.clip.json`; nothing blocks on them |
| NO new champion animation | - | out of this sweep |

### New parts (`assets/art/VFX/parts`, soft alpha, no rectangle edge; `check_fx_edges.py` SOFT + EDGE must pass)
- `fxp_dust_soft`: an untinted grey-brown dust puff (alpha-blend material). Weight impacts, stone drops, fizzles,
  UPSET's skirt.
- `fxp_crack_soft`: a thin dark jagged stroke (HARDFACE's cracks).
- `fxp_drip_soft`: a teardrop with a soft tail (WEEP / VENOM).
- `fxp_plate_soft`: a chamfered stone slab (HOLD FAST).
- `fxp_stone_block`: a wedge stone with soft edges, made from `fx_tower_strike`'s cleanest frame by `fxclips.py`
  (whiten off, soften, feather), not generated.
- `fxp_thorn_stake`: a tapered spike, dark wood with a lit edge mask (NARROWS).
- `fxp_oath_band`: one forged half-ring (drawn twice, mirrored) with a seam mask (OATHMARK).
- (`fxp_front_soft` is NOT made: no field but PRESS sends a front down the row. PULSE's ring, GRAVE SONG's shimmer and
  UPSET's skirt are `fxp_ring_soft`, `fxp_flash_soft` and `fxp_dust_soft` stretched.)
All drawn deterministically in `tools/asset-pipeline/v2/<name>.py` on the game's material (as `seeker_press.py`,
`seeker_jaws.py`): no PixelLab.

### The champion missiles (`assets/art/Props`, one per champion; each with an `_edge` emissive mask)
`prop_quiver_arrow`, `prop_chorus_charm` (a bone charm), `prop_unbroken_chip` (a stone chip), `prop_metronome_bolt`
(a brass dart with a gear head), `prop_anvil_rivet`, `prop_tower_shard` (a stone shard), `prop_thornwall_thorn`,
`prop_oathbound_hook`, `prop_magpie_coin` (a sharpened coin). The Seeker's knife exists. Each serves the champion's
basic attack (where it throws), SPRAY-agnostic, CLOCKWORK / BACKDRAW. Drawn procedurally like `seeker_knife.py`
except THREE that need an organic silhouette: the charm, the bolt's gear head and the coin: PixelLab
`create_image_pixen` at 64 px, one generation each, then redrawn on the grid (<= 4 generations with one retry; the
budget stays). The film needs the arrow, the charm, the chip, the bolt and the hook first; the other four follow in
the tail phase.

### Generation, only where necessary
PixelLab is used for: the three missile silhouettes above. Nothing else: no VFX motion, no champion frames, no
field or affliction art. Every effect in this sweep is parts + procedure on existing performance classes plus the
three small new kinds (Gesture, Shock, Drop) and the REACH mode.

---

## 7. Audio plan

Every cue is built from CC0 recorded foley by a `make_<family>.py` in `tools/asset-pipeline/` (sources listed in
`foley/<family>/SOURCES.md`, the licence checked on each sound's page), byte-reproducible, measured K-weighted against
the references in the fight's own mix (the PRESS method), candidates in `audio_history/`. Synthesis only for a sub or
a resonance inside a foley cue. Volumes are pre-master (x0.8). Resolution chains stay most-specific first:
`sfx_<champ>_<skill>_<moment>` -> `sfx_<skill>_<moment>` -> archetype -> generic.

**Two pin levels.** A shipped file is pinned by SHA-256 as SHIPPED (a change is a deliberate commit, never a
regeneration). HUMAN-APPROVED is reserved for the owner's word (the five references' cues keep it); no sweep cue
claims it before the owner has heard the film. The test names say which (`..._is_shipped` / `..._is_human_approved`).

**Order: archetypes first, identity second.** PRESS's single tick took a day and three candidates; ~58 bespoke cues
in the last phase with the owner hearing them only in the film is the schedule risk of the sweep. So:
1. **Phase 1 (with the basics): the eight ARCHETYPE cues** the resolution chain already names or needs, so every item
   has a non-generic sound from its first film: `sfx_fist_hit` (a padded fist), `sfx_blade_hit` (a short cut into
   leather), `sfx_stone_hit` (a stone on packed earth), `sfx_wood_hit` (a wooden thump with a rim), `sfx_throw_release`
   (a cloth whip + air), `sfx_air_release` (an air push, no tone: PULSE, REPAY, DRINK's intake family), `sfx_field_tick`
   (a quiet material settle: MIRE / WILT deepen, HOLD FAST's set until its own), `sfx_cloth_commit` (a boot plant +
   cloth: every commit until its own). One candidate each, measured against the references.
2. **Per-item IDENTITY cues, built as their phase films** (only where the item's identity is in its sound): the ten
   swing hits (5.18-5.27), the seven signature contacts (BLOW, HARDFACE, PAYING WORK, CLOCKWORK, NARROWS, BACKDRAW,
   SLOW FALL), `sfx_execute`, `sfx_undying`, `sfx_grave_song_tick` + `_toll`, `sfx_call_open`, `sfx_oathmark_open`,
   `sfx_hold_fast_set`. Everything else in the tables below resolves to an archetype until (and unless) a film says
   the archetype reads wrong.

### Basic-attack hit family (T1, 0.36 each; one cue per hit; releases 0.16-0.18)
| Key | Materials |
|---|---|
| sfx_seeker_swing_hit | a short blade cut into leather, a faint steel edge |
| sfx_anvil_swing_hit | a padded heavy fist (a boxing bag), a low thud |
| sfx_metronome_swing_hit | a dry knuckle hit on wood, a sharp click |
| sfx_tower_swing_hit | a mallet on packed earth, a short gravel tick |
| sfx_thornwall_swing_hit | a wooden shield thump with a steel rim rattle |
| sfx_magpie_swing_hit | a quick knife nick, a short cloth tear |
| sfx_quiver_swing_hit / sfx_quiver_loose | an arrow into hide + a wooden clack / a bowstring twang |
| sfx_chorus_swing_hit / sfx_chorus_toss | bone clatter on hide / a light toss whip |
| sfx_unbroken_swing_hit / sfx_unbroken_toss | a stone chip on rock / a short heavy toss |
| sfx_oathbound_swing_hit | a chain whip crack + two link rattles |

### Skill cues (identity cues marked ID; the rest resolve to an archetype until built)
| Key | Moment | Level | Materials / note |
|---|---|---|---|
| sfx_blow_commit / sfx_blow_hit (ID) / sfx_blow_hit_flatten | commit / contact / FLATTEN contact | 0.30 / 0.52 / 0.52 | cloth whip + boot plant; sandbag on timber + mallet on plank + floor thud; + a short dry crack |
| sfx_execute (ID) | FINISH's kill | 0.56 lead | one heavy clean impact, a short dark tail, no crack |
| sfx_carry_land | a carry that kills | 0.16 | a small soft tick |
| sfx_repay_release / sfx_repay_hit / sfx_repay_fizzle / sfx_repay_bank | commit / contact (bank-scaled 0.30-0.48) / zero cast / BANKED | 0.26 / 0.30-0.48 / 0.16 / 0.36 | air draw + cloth snap; canvas slap + padded hit + ember hiss; dry puff; iron plate set + air draw |
| sfx_call_open (ID) | the call | 0.36 | a damped wooden tongue drum tap + a soft cloth whip |
| sfx_weep_arrive | the transfer's arrival | 0.18 | one wet drop on stone |
| sfx_pulse_release / sfx_pulse_hit (+ ticks) | stomp / arrival | 0.24 / 0.40 (0.16) | a boot stomp + an air push; a drum skin + a sandbag |
| sfx_mire_deepen | a tick that deepened | 0.18 | mud + gravel shift |
| sfx_drink_commit / sfx_drink_hit / sfx_drink_hit_glut / sfx_drink_intake | commit / grab / GLUT / arrival | 0.28 / 0.44 / 0.48 / 0.22 | cloth; damp cloth squeeze + low pull; padded heavy impact; bellows air draw |
| sfx_wilt_deepen | a deepening tick | 0.14 | dry leaves + a small paper crumple |
| sfx_hardface_commit / sfx_hardface_hit (ID) / sfx_hardface_soften | commit / contact / once per kill | 0.32 / 0.52 / 0.22 | boot plant + cloth; padded heavy fist + damped anvil thud; plaster / clay crack (0.22: under the affliction ceiling 0.238) |
| sfx_grave_song_tick (ID) / sfx_grave_song_toll (ID) | every tick / the first tick after the dead count rose | 0.26 -> 0.32 / 0.34 | bone / wood chime rattle; a damped large bell (the game's ONE bell); REQUIEM heavier; CHANTRY rattle 0.24 |
| sfx_clockwork_tick / _release / _hit (ID) (+ ticks) / _chime | telegraph x3 / release / contact / HELD NOTE | 0.14 / 0.30 / 0.46 (0.18) / 0.46 | escapement tick; latch + spring; small hammer on brass; a struck tuning fork / brass bar (a note, not a bell) |
| sfx_hold_fast_set (ID) | the plate | 0.30 | stone set on stone + a steel latch |
| sfx_slow_fall_hit (ID) / _hit_small (+ ticks) | contact / COURSES, DRYSTONE | 0.40 / 0.24 (0.16) | a flagstone on earth + gravel scatter; a smaller stone at the fast-tick ceiling |
| sfx_backdraw_loose / sfx_backdraw_hit (ID) (+ ticks) | loose / arrival | 0.30 / 0.40 (0.16) | bowstring twang + arrow whip; arrow into hide + wooden clack |
| sfx_narrows_answer (ID; tiers 1-3) | the snap | 0.42 | stake into soil + bramble rustle; +1 / +2 layered stake hits, pitch -0.02 per tier |
| sfx_oathmark_open (ID) | the open | 0.34 | iron clasp + chain shiver |
| sfx_paying_work_commit / _hit (ID) / _take | commit / cut / arrival | 0.28 / 0.52 (boss: + a 0.20 tick at +40 ms) / 0.22 | cloth whip; knife into leather + coin-purse snatch; coins settling |
| sfx_undying (ID) | the refusal | 0.60 lead | air intake + heartbeat thump + a low wooden drum (no bell) |
| sfx_spray_release / _hit / _tick | the shared SPRAY family for other champions | 0.40 / 0.50 / 0.20 | built once from the same recipe as the Seeker's (a thrown-object volley), measured to the same loudness |

### Silence decisions (intentional)
Bleed / reflect / carry / deadweight ticks; MIRE's regular ticks; WILT at the floor; HOLD FAST at the cap (the rim
pass) and its roots; the SLOW FALL fall itself (no whistle); CALL / OATHMARK closes and the empowered accent; CHARGE
storing; WEEP's ticks and stain; the generic heal receive; every affliction at rest; GRAVE SONG's toll on a tick
where nothing new has died. The information in each case is on screen or in the next action's own sound.

### Mix rules (pinned by a `sweep_audio_hierarchy_test`)
Ordinary hits 0.34-0.38 < skill contacts 0.40-0.50 (<= SPRAY) < signature contacts 0.52 (< HARD HANDS 0.55) <
major 0.56-0.62. Field ticks <= 0.34 (a tick faster than 1.5 s <= 0.24), afflictions <= 0.22, never lead. Actives
duck 0.45 from release / commit to +160 ms; reactions and fields take the duck; the quiet rule x0.6; a wave-ending
tick plays its cue out. Per-key throttle gaps are added for the new families (hit families 60 ms, ticks 90 ms);
unthrottled: contact ticks only. Three bells would be a motif nobody chose: GRAVE SONG's toll is the only bell;
CLOCKWORK's chime is a fork, UNDYING's weight is a drum. `film_audio.py`'s hand-mirrored throttle table is replaced
by `tools/asset-pipeline/sound_throttle.json`, written by `sound_throttle_table_test.cs` from `SoundBank` itself
(the test fails when the file is stale).

---

## 8. Implementation order

No big bang: the first film comes from the smallest change that removes the worst defects, and every later phase
adds its own infrastructure right before it needs it.

**Phase 0: the generic-path fixes, filmed first (one day, no new art).** Bleed / Reflect / Carry / Deadweight
Strikes never flash / puff / thud and print at quiet grade (ends the bleed strobe); N creatures never make N
`sfx_hit`s (one cue per Strike batch); the same-ms LAST-OWNER rule (section 3); callouts say the skill name for
Actives and nothing for the rest; no pitched thud for a no-damage reaction; a Skill with no Strike in its batch
draws nothing (BACKDRAW's phantom); the WEAVER echo flag; the heal column replaced by the chest glow with "+N"
summed; the ShieldGained CLAIM hook; the `trap` clip no longer loaded or committed for reactions. Film: the default
fixture and `fightmulti` before / after. This is also the regression baseline for every Seeker reference pair
(`action_regression.py`, extended to the reaction, field and mark traces): the five pairs must be byte-identical
across every later phase.

**Phase 1: the ten basic attacks + the first missiles + the archetype cues.** `clip_markers.py`; the ten `attack`
`.clip.json` files and the four baked-VFX cleans; `SwingRecipes.For(champion)` with the step-in offset (no root
motion); `prop_quiver_arrow`, `prop_chorus_charm`, `prop_unbroken_chip`, `prop_oathbound_hook`; the eight archetype
cues (section 7). The champion-agnostic tier `ByAnySkill` lands here too (no colour parameter on any closed layer:
JAWS / PRESS / BRAND resolve byte-identical). Film: Part 1 segment A at TEMPO 60 plus the ten swing chapters. Risk:
a step-in clip that reads as a slide on a wide silhouette (the Quiver / Thornwall): the offset cap is 20 %.

**Phase 2: fields, on `FieldLayers`.** `FieldLayers` replaces `FieldRoles.Choose` right before the first field
recipe: a LIST of at most two presented fields (each recipe declares its subject and its draw slot: ground /
behind-figures / on-bodies / over-creatures / on-hunter / above); `HoldAura` and `PulseAuraOnClock` deleted; the
shared-tick rules (PRESS owns the squash; a reaction makes fields give way). Then MIRE, WILT, GRAVE SONG, HOLD FAST,
SLOW FALL (the Drop kind), PULSE (the Gesture + Shock kinds), PRESS and BRAND agnostic; the Seeker's `cast` and
`chorus_cast` timing. Risk: PRESS beside MIRE and WILT must film identical for PRESS (the Phase 0 regression); the
sprite budget is <= 12 per field layer (PRESS's 10 is the bar), two layers at most.

**Phase 3: heavy melee + the conventions.** The Seeker's `strike` / `transformation`, `anvil_strike`,
`magpie_strike` timing; BLOW, HARDFACE (with UPSET's skirt and the soften, keyed to kill batches), PAYING WORK, DRINK
(REACH mode), EXECUTE, CARRY, ARMED, GUARD-BREAK, RETURN; the signature contact cues. Risk: BLOW on the Seeker's
knife swing reading light: the hold, the impact and the cue carry the weight; if the film says no, the fallback is a
2-frame key-pose edit of the contact frame (one PixelLab edit), not a new clip.

**Phase 4: projectiles + reactions.** `metronome_projectile` and `quiver_projectile` timing; `prop_metronome_bolt`;
CLOCKWORK (+ telegraph, which needs the `ChampionStateLayer` skeleton: built here, minimal), SPRAY agnostic, BACKDRAW,
NARROWS, OATHMARK's open (its bands wait for Phase 5's layer), WEEP's transfer, JAWS agnostic. Risk: the reaction
layer interface (`IReactionLayer`) must not touch JAWS' class: new reactions are siblings; the HuntScreen list holds
the interface.

**Phase 5: self-states + afflictions + procs.** `AfflictionLayer` (per-creature slots, `MarkPoints` seating, the draw
TINT and SQUASH hooks beside the curse's `PassDue`; alloc 0) and the full `ChampionStateLayer` + `ReturnStream`
right before: then CALL (+ the Seeker's `mark` timing, the Gesture kind shared with REPAY and PULSE), REPAY, WEEP's
pool, WILT's wither, HARDFACE's cracks, OATHMARK's bands, DEADWEIGHT, UNDYING, the ECHO mode, CHARGE's accent, bleed /
reflect; the Core hooks C1 / C2 / C3 / C3b with fingerprint tests and BRAND's builder matched by slot. Risk: three
states on one figure (placed in three places, section 2G).

**Phase 6: the film.** The remaining identity cues, the hierarchy test, `sound_throttle.json`, the full showcase
film (section 9) rendered from per-take runs.

**Tail (after the film, before the owner's review closes):** the ~38 remaining `.clip.json` files, the four remaining
props, the in-process sequencer if time remains, the legacy folder's deletion commit after acceptance.

Tests per phase mirror the references: recipe isolation pins (every skill resolves to its own recipe on every
champion; the Seeker's five unchanged), alloc-0 loops for every new layer, text pins for draw order, contact-on-the-
beat timing tests, the volume-hierarchy test, the SHA-256 SHIPPED pins, `check_fx_edges.py` on every new part (and
added to `check_all.sh` once the retired strips are out of its path).

---

## 9. Showcase script

### The rig: per-take runs, concatenated (the proven path)
- The in-process sequencer is NOT a gate: `films_press.sh` / `films_brand.sh` already pose one take per process
  with `RH_SHOT_SEQ`, `RH_SHOT_SEED=7`, `RH_PRESENT_TRACE=1` and the disk guard; `films_sweep.sh` generalises that
  pattern (`take name hunter build variation seek count stride ENV...`), renders each take's sound from its trace
  through `film_audio.py` with `sound_throttle.json`, and `showcase_film.py` concatenates the takes with ffmpeg,
  laying each caption (two plain lines, no abbreviations) for the first 1.5 s and 0.5 s of black between takes.
- **`RH_SHOT_BUILD=<skill>[@Source],<skill>[@Source],...` (new, small)**: the take's FULL four-slot build, applied
  after `RH_SHOT_HUNTER`: the loadout is cleared and each skill set with `SetSkill` per slot in order; the signature
  must be among them; a build the rule refuses (`Build.WouldRefuse`: > 2 Active or > 2 Passive) or a slot that fails
  to set is REFUSED LOUDLY (the take exits non-zero), never filmed. This replaces guessing what `RH_SHOT_SWAP` leaves
  behind: today `RH_SHOT_HUNTER=quiver` sheds HARD HANDS and `EnsureSignature` fails silently on the default fixture
  (SPRAY + PRESS + JAWS + BACKDRAW = 3 passives, `LoadoutRepair.cs:145-156` returns -1), so the Quiver would fight
  WITHOUT BACKDRAW.
- **Seek**: `DevSeekBefore` generalised to "the first Skill of skill X", "the first Aura of slot N", "the first
  EnemyDown", "the first Strike with Hit=Bleed", "the first EnemyStrike that downs", "the Nth Beat" (`RH_SHOT_SEEK`).
  "Swings only" is not a fixture (`fightswing` only sets `DevSwingPhase`): the swing chapters seek the beats BEFORE
  the signature's first cast (a 6-beat signature gives five swings; the signature is mandatory and stays).
- Where a seed must produce a crit (chapter 31) or a low creature (12, 35), `showcase_seed.py` runs the Core fight
  headless for seeds 1-200 and picks the first that meets the take's predicate; the chosen seed is committed in
  `films_sweep.sh`.
- Audio: the trace render stays (SoundBank is off under the rig); the throttle table is generated, not mirrored.
- Every take's trace is committed with the film under `production/qa/evidence/vfx-sweep/<tag>/`, with the measured
  table (sprites, draws, alloc per layer) as PRESS and BRAND's evidence has; one `--slow` 0.25x render of the takes
  marked (slow) for the owner's frame-by-frame look.

### ONE film: the gameplay reel first, the catalogue as chapters
The brief asks for one gameplay showcase film. **Part 1** is real builds at speed: the owner sees every rule under
load first (the yield / quiet / duck rules, the shared ticks, the step-ins at TEMPO 60). **Part 2** is the catalogue,
one chapter per item with its secondary state, as evidence. Total ~9 min. Every take declares its full build
(<= 2 Active, <= 2 Passive).

**Part 1: mixed combat (~2.5 min)**
| # | Caption | Hunter / build | Pose | Duration |
|---|---|---|---|---|
| A | THE SEEKER at the fastest TEMPO / every action, every rule | seeker: HARD HANDS, SPRAY@Mind, PRESS@Body, JAWS@Shadow (the default fixture) | the fastest fixture (RHYTHM + VOLLEY, TEMPO 60); swings yielding into SPRAY | 30 s |
| B | THE ANVIL in a swarm / cracks, weights, deaths | anvil: HARDFACE, SPRAY@Body, MIRE@Nature, WILT@Mind | swarm 5, DEADWEIGHT; kills mid-wave (the soften), both fields drawing | 30 s |
| C | THE CHORUS with two fields / the song and the mire, and a cast on a tick | chorus: GRAVE SONG, MIRE@Nature, PULSE@Spirit, DRINK@Nature | creatures 5; a PULSE cast posed on a MIRE tick's ms (`showcase_seed.py`); the shared-tick rules, x0.6 | 30 s |
| D | THE QUIVER with LOOSE AGAIN / deaths that cascade | quiver: BACKDRAW, SPRAY@Mind, CALL@Spirit, PRESS@Body | `fightmulti` low-health creatures; amplified volleys; a cascade | 25 s |
| E | THE MAGPIE on a boss / with REPAY, WEEP and BRAND | magpie: PAYING WORK, REPAY@Machine, WEEP@Shadow, BRAND@Shadow | `RH_SHOT_BOSS`; the heal return, the bank, the pool and the curse on one boss body; a boss cut every cast at T3 | 30 s |
| F | THE UNBROKEN takes everything / the wall, the plates, UNDYING | unbroken: HOLD FAST, JAWS@Spirit, BLOW@Body, REPAY@Machine (BANKED) | Fast biters; ticks below and at the cap (the rim pass); a lethal bite at the end (SECOND WIND) | 25 s |

**Part 2: the catalogue (~6.5 min; each item once, with its secondary state)**
Order: basic attacks first (the game's floor), then heavy melee, projectiles, reactions, fields, afflictions,
self-states, procs. Every chapter: 2 s before the anchor, the anchor, the state shown, 1-2 s after. Unless stated the
build is: for an ACTIVE signature (seeker, anvil, metronome, magpie) signature + SPRAY@Mind + PRESS@Body +
JAWS@Shadow; for a PASSIVE signature (quiver, thornwall, oathbound, chorus, tower, unbroken) signature + PRESS@Body +
SPRAY@Mind + BLOW@Body (a passive signature plus JAWS plus PRESS would be three passives). The named swaps replace
one of those slots (always <= 2 Active + 2 Passive; `RH_SHOT_BUILD` refuses anything else).

| # | Caption (line 1 / line 2) | Pose | Seek / duration |
|---|---|---|---|
| 01 | THE SEEKER hits with his blade / one hit every beat | seeker, default build, creatures 3 | Beat 1, the five swings before HARD HANDS' first cast, 6 s (slow) |
| 02-10 | THE ANVIL / METRONOME / TOWER / THORNWALL / MAGPIE / QUIVER / CHORUS / UNBROKEN / OATHBOUND hits with ... | hunter = each; an Active signature: signature + BLOW@Body + PRESS@Body + JAWS@Shadow; a passive signature: signature + PRESS@Body + BLOW@Body + DRINK@Nature (two 6-beat actives; fields, reactions and CLOCKWORK's clock never take a beat, so the opening beats are swings), creatures 3; the metronome's caption adds "the first hit on each enemy is doubled" | Beat 1, the swings before the first cast, 6 s each |
| 11 | BLOW: one heavy hit every 6 beats / FLATTEN: it goes through armour | seeker, BLOW@Body for SPRAY, variation FLATTEN+TOLL+BREAKTHROUGH, `fightmulti`, an armoured archetype, enemy health so the blow kills with excess | Skill(hammer_blow) -600, 7 s (the carry line) (slow) |
| 12 | BLOW with FINISH: a second kill / on the weakest enemy | same, variation FINISH+BRINK+CLEAN CUT, a creature under 25 % at the beat (seed searched) | Skill(hammer_blow) -600, 7 s |
| 13 | BLOW with TRAIL / the next hit goes through armour | variation FLATTEN+TRAIL, creatures 2 | Skill -600, 8 s (the following swing) |
| 14 | HARDFACE: the heavy blow / each death softens the rest | anvil, `fightmulti`, creatures 4 | Skill(sig_anvil_hardface) -600, then hold through the first EnemyDown, 10 s (slow) |
| 15 | HARDFACE with UPSET / the weight falls on everyone; PLANISH with PEENING: a crack without a kill | anvil, variation UPSET+THIRD DROP, creatures 4; then PLANISH+PEENING, creatures 3 | Skill -600, 6 s each |
| 16 | PAYING WORK: a cut, and she takes it back / on a boss, a bigger cut | magpie, hunter hurt (enemy baseline), then `RH_SHOT_BOSS` take | Skill -600, 6 s; boss take 6 s |
| 17 | PAYING WORK with LIGHT FINGERS / a touch on everyone | magpie, variation LIGHT FINGERS+FULL HANDS, creatures 5 | Skill -600, 6 s |
| 18 | DRINK: he reaches, and life comes back / then the wave's limit is spent | seeker, DRINK@Nature for SPRAY, Bruiser archetype, hunter hurt, long wave | Skill #1 -600, hold to the Skill after the ceiling (the number plain), 14 s |
| 19 | DRINK with GLUT / a fed blow, bigger at full health; SIPHON with TRICKLE: every swing sips | variation GLUT+RIPE 6 s; then SIPHON+PUMP+TRICKLE, hunter hurt, 7 s | Skill -600 each |
| 20 | CLOCKWORK: the clock runs down / bolts strike three at once | metronome, creatures 4 | Skill -900 (the telegraph), 7 s (slow) |
| 21 | CLOCKWORK with HELD NOTE / one chime on all of them; ROLL with RIM SHOT: three bolts each | variation HELD NOTE+SPARE SHOT, creatures 2, 7 s; then ROLL+RIM SHOT+OFF BEAT with a kill mid-volley, creatures 3, 7 s | Skill -900 each |
| 22 | SPRAY on THE QUIVER / the same volley, her arrows | quiver, the passive-signature default = BACKDRAW, PRESS@Body, SPRAY@Mind, BLOW@Body | Skill(volley_spray) -600, 6 s |
| 23 | BACKDRAW: a death sends arrows / CLEAN SWEEP: on the whole row | quiver, build as 22, creatures 4; then variation CLEAN SWEEP+SECOND STRING | first Skill(sig_quiver_backdraw) with Strikes -400, 6 s each (slow) |
| 24 | BACKDRAW with ONE SHAFT / one heavy shaft, and it bleeds | variation ONE SHAFT+BROADHEAD | 7 s (the pool drips) |
| 25 | NARROWS: it bit him, the road narrowed / bigger each time | thornwall, Fast attack bias, enemy bite low, hold answers #1-#4, 14 s | first Skill(sig_thornwall_narrows) -300 (slow) |
| 26 | NARROWS with BRAMBLE / on everyone; CHOKE with SECOND STAKE: two at once | variation BRAMBLE+UNDERGROWTH, creatures 4, 6 s; then CHOKE+SECOND STAKE, 6 s | |
| 27 | JAWS on THE ANVIL / the same bite; his Source in the number | anvil: HARDFACE, SPRAY@Mind, PRESS@Body, JAWS@Body | first Skill(snare_jaws) -300, 6 s |
| 28 | REPAY: he keeps what hurt him / and it snaps back; and when nothing was kept, nothing goes | seeker: HARD HANDS, REPAY@Machine, MIRE@Nature, PRESS@Body (JAWS out so bites reach health); a ZERO cast first, then a small bank, then a large bank (three casts), 16 s | Skill #1 -2500 |
| 29 | REPAY with BANKED / it becomes a plate instead | variation BANKED+STANDING+CARRIED (the wave-open plate shown at the take's start) | wave start, then Skill -600, 9 s |
| 30 | CALL: every hit lands bigger / for six seconds | seeker, CALL@Spirit for SPRAY, HARD HANDS kept, creatures 3 | Skill -600, hold to +6.5 s (the glow goes out at the reported ms), 9 s |
| 31 | CALL with SPEND / three hits exactly, a critical keeps one | variation SPEND+COUNT+PERFECT CLAUSE, seed chosen for a crit in the window | Skill -600, 9 s |
| 32 | CALL with STEADY / stronger each cast, for the wave | variation STEADY+PILLAR+FOUNDATION, two casts | wave start, 14 s |
| 33 | OATHMARK: his oath binds them / his hits land bigger until it closes | oathbound: OATHMARK, SPRAY@Mind, PRESS@Body, BLOW@Body, creatures 4; then SEALED WORD vs OPEN WORD+FIRST WORD; then OPEN WORD+SAID AGAIN held to the cap | first Skill(sig_oathbound_oathmark) -300, 6 s each; SAID AGAIN 12 s |
| 34 | PULSE: one stomp hits everyone / THRONG: bigger with the crowd | seeker, PULSE@Spirit for SPRAY, Swarm wave >= 4, variation THRONG+CROWDED | Skill -600, 6 s (slow) |
| 35 | PULSE with SHARE / equal pieces, only on the chosen | variation SHARE+NARROWED, then +BALANCE on a wave with low creatures | Skill -600, 6 s each |
| 36 | MIRE: the pack stands in a mire / it bites more slowly | seeker, `fightaura` (MIRE woven), creatures 5, variation NUMB+DEEPEN, 10 s | first Aura(field_mire) -1200 |
| 37 | MIRE with TEEMING and REMNANT / the dead stay in it | variation TEEMING+REMNANT, kills mid-wave | 10 s |
| 38 | WILT: they wither and bite softer / SUP: a little comes back | seeker, `fightinspect`, hunter hurt, variation SUP+BALM, 8 s | first Aura(drain_wilt) -1200 |
| 39 | WILT with SHRIVEL / one body, and it follows the front | variation SHRIVEL+HOLLOW, a front kill mid-wave; then SHRIVEL+GAUNT on a boss | 8 s + 6 s |
| 40 | GRAVE SONG: the song is everywhere / the dead hum it louder | chorus, creatures 5, hold from tick 1 past two deaths (the toll on the tick after each), 12 s | first Aura(sig_chorus_grave_song) -1200 |
| 41 | GRAVE SONG with CHANTRY / the wave is hushed | variation CHANTRY+HUSH, an EnemyStrike after deaths | 8 s |
| 42 | HOLD FAST: a plate every two seconds / a full wall is simply full | unbroken, bites that drain the shield; a tick below the cap, a tick at the cap (the rim pass), a break | first Aura -1200, 12 s |
| 43 | HOLD FAST with DEEP ROOTS / roots feed him with it | variation DEEP ROOTS+TAPROOT, hunter hurt, four pulses | 10 s |
| 44 | SLOW FALL: a stone falls, on time / he never moves | tower, creatures 2, a tick that kills | first Aura -1200, 8 s (slow) |
| 45 | SLOW FALL with COURSES / ONE STONE: one slab on three | COURSES+DRYSTONE 6 s; ONE STONE+CAPSTONE+FULL COURSE creatures 3, 7 s | |
| 46 | WEEP: a death leaves bleeding / it sits on the next one | seeker, WEEP@Shadow for JAWS, `fightmulti`, an early kill, several ticks, a bleed kill | first EnemyDown -400, 10 s |
| 47 | WEEP with TORRENT / with CARRION | TORRENT+SPILLWAY+FLOOD 7 s; CARRION+DREGS+ONSET (SPRAY in frame) 7 s | |
| 48 | PRESS on THE CHORUS / BRAND on THE TOWER / the same picture; their Source in the number | chorus with PRESS@Nature; tower with BRAND@Machine | first Aura -600, 6 s each |
| 49 | DEADWEIGHT: a third of the hit stays / and lands with the next | anvil, Bruiser archetype | first DeadweightStored -300, 6 s |
| 50 | UNDYING: the killing bite lands / he refuses it | unbroken (SECOND WIND), `RH_SHOT_ENEMY` lethal bite | the downing EnemyStrike -600, 5 s |
| 51 | THORNS and the poison pool / quiet, never a flash | a THORNS keystone build vs Fast biters; a VENOM build | first Reflect Strike -300, 6 s; first Bleed Strike, 6 s |
| 52 | WEAVER: the next skill echoes / once, quietly | seeker + WEAVER keystone with SPRAY and HARD HANDS | the echoed Skill -600, 6 s |
| 53 | Heal and shield / the plain receive, the wall | a lifesteal swing build hurt; `RH_SHOT_SHIELDFX` gain / absorb / break | 8 s |
| 54 | CARRY: the excess jumps on / one line, on the beat | seeker, BLOW+BREAKTHROUGH (the chapter 11 take's carry at 0.25x) | (slow) 4 s |
| 55 | A cast on a field's tick / each keeps its own | the Part 1 C take's PULSE-on-MIRE ms at 0.25x | (slow) 4 s |
| 56 | CHARGE and REND / the dump lands bigger, once | seeker with the CHARGE keystone + REND, HARD HANDS | the first Charge drop -600, 6 s |

Chapters 22, 27 and 48 read "the same skill on another champion": no redesign, the Source shows in the number. The
Part 1 takes are filmed first (they are the acceptance of the rules); the chapters are cut from their own takes.

---

## Critique log (revision 2, 2026-10-03)

The adversarial review's 18 corrections, with the decision on each. All code claims were re-read at `ae20a7fd`
before acceptance.

1. **Builds the rule refuses (ACCEPTED).** `MaxPassiveSkills = 2`, a passive is any Field or Reaction. Part 1 C and F
   each held three passives; the Quiver chapters would have fought without BACKDRAW (`EnsureSignature` returns -1
   silently on the default fixture); `fightswing` is not "swings only". Every take now declares its full build through
   a new loud `RH_SHOT_BUILD`; "swings only" seeks the beats before the signature's first cast; the "five layers on
   one tick" stress case is gone, and `FieldLayers` holds at most two.
2. **HARDFACE's soften on PRESS ticks (ACCEPTED).** PRESS emits Break / DefenceNow every tick (`SoloBattle.cs:2358`).
   The soften now reads Break only in a kill batch (after an EnemyDown) or a HARDFACE Skill batch (PEENING); an Aura
   batch's Break draws nothing from it. `DrawBreakBadge` stays as the generic read of PRESS's and PEENING's breaks.
3. **PULSE / GRAVE SONG / UPSET copying PRESS's front (ACCEPTED).** Faster and brighter is a parameter, not a picture.
   PULSE is a radial ground ring from a stomp with simultaneous flicks on the beat; GRAVE SONG sends nothing (the dead
   hum, a vertical shimmer on the tick); UPSET is a dust skirt along the ground. `fxp_front_soft` is not made.
   Rejected only the sub-claim that "nothing needs to travel" for PULSE: a ring from his feet still has to reach the
   row, and it does, as a ground phenomenon arriving on the beat, not a front travelling level.
4. **The EMPOWERED-HIT accent at signature size on every hit (ACCEPTED).** The outlined number and a flash rim are the
   primary signal; the ring is <= 0.45 of the height at alpha 0.3, only on the first amplified hit after an open and
   on SPEND's spends (and REND's dump: countable); under SEALED WORD only banded creatures (`ampWholeWave || front`).
5. **PAYING WORK on a boss at T4 every 5 beats (ACCEPTED).** `BossPower` fires on every cast of a boss wave; a cadence is
   never Major. T3 with a bigger cut (two slashes, impact 0.8, flash 0.50, hit 0.52, no white-hot frame, no lead);
   T4 is once-per-wave / run only. The rule is now written into section 3.
6. **Basic melee attacks carrying the body the full gap every beat (ACCEPTED).** ADR-012 closed the mirror problem at
   ~20 % travel and recorded the row gap as a layout limitation. Basics step in 20-25 % as a draw offset over the
   clip's own frames (no root motion, no retreat machinery); the Magpie's basic the same. The TEMPO 60 "never home"
   regime no longer exists to measure.
7. **C3's timeout close cannot be emitted (ACCEPTED).** `ampUntil` is read lazily (`:1411`); nothing runs at expiry.
   `AmpWindow` carries the absolute close ms on every change (open, refresh, the MIND stretch at `:2980-2985`, the
   SPEND count-close at `:1430`); the screen ends the state itself at the reported ms.
8. **REPAY as a thrown mass (ACCEPTED, with one precision).** A ProjectileActionRecipe for a Core hit with no travel
   on the champion the film shows SPRAY on is SPRAY with a ball. REPAY is a `GestureRecipe` (the `cast` strip in
   place) plus a reversed `ReturnStream` strand from the guard shoulder arriving on the beat. The review's "no flight
   before the beat" and "<= 120 ms arriving on the beat" cannot both hold; the strand leaves at -100 ms, which is a
   strand's travel, not an object's flight (nothing is in the hand, nothing is in the air). The Gesture kind is
   shared with CALL and PULSE, so no performance kind is spent on REPAY alone.
9. **Idle states that are not quiet (ACCEPTED).** HOLD FAST at the cap is one silent 150 ms rim pass, no plate;
   CALL's glow and REPAY's embers breathe on the skill's ready ping (or every 4th beat), never per beat.
10. **DRINK's grab lunging a self-pose (ACCEPTED).** No root motion: `MeleePerformance` gains a REACH mode (root
    motion 0, the contact bridged by a streak), used by DRINK and the Oathbound's lash. The return stream and GLUT's
    swollen blow stay.
11. **Four corpse-sourced streaks with one shape (ACCEPTED).** CARRY is an instant 2-frame line with the number on the
    beat (the 120 ms echo and its exception are gone); HARDFACE's spread is a dark ground ripple; WEEP keeps the only
    slow wet strand; BACKDRAW's pull runs the other way. Written as a convention in section 4.
12. **Audio scope and order (ACCEPTED).** Eight archetype cues in Phase 1 so every item has a non-generic sound from
    its first film; per-item cues only where identity needs them (the ten swing hits, the seven signature contacts,
    EXECUTE, UNDYING, GRAVE SONG, CALL, OATHMARK, HOLD FAST); SHIPPED pins, HUMAN-APPROVED reserved for the owner's
    word; `sfx_hardface_soften` 0.22; COURSES / DRYSTONE 0.24 and a general "faster than 1.5 s <= 0.24" rule; one
    bell (GRAVE SONG's toll, only on a tick after the dead count rose), CLOCKWORK's chime a fork, UNDYING a drum.
13. **Phase 0 as a big bang (ACCEPTED).** The generic-path fixes are Phase 0 alone and filmed first (they are also the
    regression baseline); `FieldLayers` lands at the start of Phase 2, `AfflictionLayer` / `ChampionStateLayer` at the
    start of Phase 5 (a minimal state-layer skeleton earlier for CLOCKWORK's telegraph); ~22 timing files now, ~38 in a
    tail; the in-process sequencer is a tail item, `films_sweep.sh` + ffmpeg is the path.
14. **Asset deletions breaking the QA twin (ACCEPTED).** `fx_seeker_projectile_strip8_512` keys SPRAY's legacy
    composite (`ProjectileLook.cs:234`); `fx_aura` is the legacy field. Nothing is deleted: retired strips move to a
    legacy folder excluded from the consumer gate; deletion is a follow-up commit after acceptance.
15. **MIRE's sink moving the draw box (ACCEPTED).** ADR-006: one geometry. The sunk read is the band's top strip
    occluding the feet (height = depth); the bite drag is the top strip stretching toward the biter for 2 frames.
    WILT's droop is likewise a draw-time squash with the actor box unchanged (made explicit).
16. **JAWS re-tinted by Source while BRAND stays Shadow (ACCEPTED and EXTENDED).** Inconsistent, and a colour-table
    refactor on a closed pinned layer edits an approved picture. JAWS is byte-identical everywhere, and so is PRESS
    (the review left PRESS's Source parameter standing; the same reasoning applies: `fab25919` is one approved picture).
    The slot's Source shows only in the number. This also removes the colour regression films from Phase 1.
17. **Same-ms ownership (ACCEPTED).** The field fork's Aura + Strikes precede a cast's Skill + Strikes on a shared ms,
    so "a Strike in a Skill's batch is the skill's" would have stolen MIRE's. A Strike belongs to the most recent Aura
    or Skill at its ms (`auraAtMs` becomes a last-owner).
18. **Showcase gaps and one film (ACCEPTED).** One film: Part 1 is the gameplay reel at speed (the spine), Part 2 the
    catalogue as chapters. Added: CHARGE / REND (56), SIPHON + TRICKLE (19), REPAY's zero-bank fizzle (28), PLANISH +
    PEENING (15), ROLL + RIM SHOT (21), SAID AGAIN (33), a PULSE cast on a MIRE tick's ms (C and 55), CHOKE + SECOND
    STAKE (26), and a slow CARRY chapter (54).

Kept as written (the review's "strong" list): the classification table and the derived conventions (RETURN,
SHIELD, DROP's silent telegraph, EXECUTE, CARRY's provenance), the hierarchy with reference-tied ceilings pinned by
a test, "a tick never flashes", "N creatures never make N sounds", the silence list, skill-name callouts for actives
only, the three information-only Core hooks with "nothing else", MIRE's sediment band, WILT's tint + droop + flakes,
HARDFACE's cracks and PEENING, NARROWS's stakes, SLOW FALL's shadow, WEEP's drips and migration with VENOM's
provenance, CLOCKWORK's telegraph, BACKDRAW's pull and the presentation-side phantom skip, OATHMARK's forged band,
the generic-path fix list, the WEAVER echo flag, parts + procedure with PixelLab for three silhouettes,
`fxp_stone_block` from `fx_tower_strike`, `clip_markers.py`, the baked-VFX cleans, the agnostic tier gated on a
`.clip.json`, the five Seeker regression pins, `showcase_seed.py`, the generated throttle table, and the two content
bugs reported rather than fixed.
