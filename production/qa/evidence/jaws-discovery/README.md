# JAWS — REACTION / TRAP discovery (ADR-011, 2026-09-25) — AWAITING APPROVAL, nothing implemented

SPRAY is the accepted PROJECTILE / TRAVEL reference and HARD HANDS the accepted MELEE / DIRECT-CONTACT reference.
The next archetype is the Seeker's JAWS (`snare_jaws`): a REACTION that takes no champion beat, answers the pack's
bite, and rearms. This is discovery only: the current behaviour measured, the questions answered, and a proposed
lifecycle for approval. No art was generated and nothing was implemented (one trace line was added:
`enemy-windup`).

Films are the real game, traced (`RH_PRESENT_TRACE=1`, `build/shots/jaws/films_jaws.sh`), the fight fixture with JAWS
woven in slot 3 (Shadow); fast TEMPO is the TEMPO build (bite, swift, quick, road_sign_2, brisk, rhythm, blitz,
volley). Timelines: `tools/asset-pipeline/reaction_timeline.py`.

## 1. What happens today, measured

One trigger (normal TEMPO, the bite at 7000; `08_timelines.md`). Everything lands on the bite's own frame (+17 ms,
the first 60 fps frame after it):

| On the bite's frame | What |
|---|---|
| the bite | `EnemyStrike` (slot 0: Core's aggregate bite), the enemy thud (`sfx_hit` 0.30, pitched down), the red bite burst on the champion |
| JAWS | its `Skill` event: the callout "SNARE", `fx_seeker_trap` (a rope ring, `cast.trap`, anchored to the whole ENEMY ROW, drawn under the figures, the whole ring tinted Shadow purple), `sfx_cast` 0.42, a pitched-up reaction thud 0.40 |
| the answer | the reflected `Strike` on the front creature: its thud 0.22, the number "−4 JAWS", its flash, a weak-hit puff |
| the champion | nothing: he keeps playing whatever clip he is in (here a swing's settle) |

- **Five sounds at one moment**, three of them generic thuds at similar weight, plus a cast breath for something that
  is not a cast.
- **The trap clip never plays.** In 70 triggers across every fixture film, `clip-start trap` appears 0 times: the
  post-bite clip is committed only when no beat is due (`TrapClipGraceMs` 380), and a fighting champion always has one.
  That path is dormant; its art (`char_seeker_trap`) shows him crouching to LAY a trap, a proactive verb, the opposite
  of an answer.
- **The ring is sized to the row.** During HARD HANDS' leap (`06a`) it is a purple ring round the whole pack for
  ~400 ms, over the signature blow. With a lone brute it sits at its feet. Nothing links the bitten champion to it.
- **The rearm is invisible.** The dock tile reads "ON BITE" and is pixel-identical armed, triggered+50 ms, rearming
  and ready (`07a`); only the trigger's cast pulse flashes. Reactions are forced "ready" (`Timing`), skipped by the
  ready crossing, and have no cooldown sweep.
- **JAWS is armed for 0 ms.** It becomes ready exactly on a bite and springs at once: normal TEMPO (rearm 3000, bites
  every 1000) and fast TEMPO (rearm 3000 / TEMPO rate = 2000) measured 0 ms in all 15 cycles. Two bites of three
  (normal) or one of two (fast) land while it rearms.
- **The row always winds up.** The whole pack winds up 883 ms before every bite and bites together; the row's contact
  frame (5/8) is the `EnemyStrike` time by construction.
- **Three pictures for one skill** (`07b`, `07c`). The dock and build icon, the only JAWS picture the player already
  knows, is a serrated iron jaw trap on a chain. The world effect is a white rope loop. The champion clip lays a trap on
  the ground. REPAY's icon is different: a thorned ring with an arrow. REPAY and JAWS share the same world effect and
  clip, because both come from the Form (`trap`).

## 2. Core facts (src/IdleXIdle.Core/Builds/SoloBattle.cs, SkillCatalogue.cs)

- JAWS: `SkillKind.Reaction`, `ReactionOn.Bitten`, `ReflectFraction` 0.5, `RearmMs` 3000, `Targets` 1. Rearm =
  max(1000, RearmMs) × CooldownMultiplier ÷ the TEMPO rate (`Champion.ReadyAt`).
- **There is no single attacker.** Every living creature's damage is summed into one bite per interval; `EnemyStrike`
  carries Slot 0 (the champion). The answer lands on the first living creature(s) front to back (`LandSpread`,
  `TargetsFor` the skill's shape: two in the fast fixture, 3 + 1).
- Order at the bite, all at one `AtMs`: shield absorbs → `EnemyStrike` (health already lost) → trait thorns →
  JAWS `Skill` → reflected `Strike`(s) → `EnemyDown` if it kills → `Kill` if it clears the wave → `Undying`/`Down`.
  JAWS still answers a bite that kills the champion; a shield that eats the whole bite does not stop the reflect
  (its basis is the attempted bite). The reflect is `HitSource.Primary` and can crit.
- Branches: NET (100 %; MESH grows it per bite, SPITE 140 %, RECOIL rearms a third sooner) keeps the verb. **IRON is a
  different verb**: no reflect, it STOPS the whole bite (6 s rearm); `EnemyStrike` is still emitted with amount 0, and
  today's pump would still draw the red bite burst on the champion. REPRISAL returns a stopped bite; PLATING turns it
  into shield.
- REPAY (`snare_repay`) is an ACTIVE skill (5 beats) on the same `trap` clip and effect: the trap clip is its cast clip.

## 3. The technical questions

| Question | Answer |
|---|---|
| Can JAWS be presented without owning the champion's committed clip? | Yes. It takes no beat; a reaction layer beside the action performance, drawing in world space, never touches the clip slot. |
| Does VfxPlayer/HuntScreen support a held/armed effect with a trigger transition? | Only a held loop (`Hold`: the field aura, the shield barrier) with no transition; the trap is a detached one-shot. A reaction performance (like `MeleePerformance`) owns its own lifecycle. |
| Can the reaction attach to the attacker? | To the creature(s) the answer lands on: the reflected `Strike` events' slots, adjacent to the JAWS `Skill` event at the same `AtMs`. There is no attacker identity in Core (the bite is the pack's). |
| Is the attacker slot preserved in BattleEvent? | No: `EnemyStrike` Slot is 0 (the champion). The struck creatures are. |
| Can trigger VFX stay pinned while actors move? | Yes (on-body effects were pinned and re-pin correctly since HARD HANDS); a reaction performance reads the champion's drawn box and the creatures' bodies each frame. |
| How is rearm represented in replay data? | Only as the triggers themselves. The ready moment (`ReadyAt`) is Core-internal; the rail's projection uses the base 3000 ms and ignores the TEMPO rate and RECOIL. |
| Enough to show ARMED vs REARMING? | Not exactly: the ready moment is not in the replay. A gameplay-neutral informational event would make it exact. |
| Audio aligned to enemy contact without another clock? | Yes: the row's contact frame IS the `EnemyStrike` time, and JAWS resolves at the same ms. |
| JAWS during SPRAY or HARD HANDS today? | Layered: effects and sounds play, the figure is not interrupted, no trap clip. The row-wide ring sits over HARD HANDS' impact. |
| A killing reflected hit? | `EnemyDown` (and `Kill` if the wave clears) at the same ms. Not reached in any fixture (the answer is 1–4 against 140+ HP). |
| The champion dies from the bite as JAWS answers? | JAWS resolves before `Down`: the answer and the fall share a frame. |

## 4. PROPOSAL — the gold-standard JAWS (for approval)

1. **The verb: BITE FOR BITE (recommended).** The skill is called JAWS. It answers being BITTEN. Its icon is already a
   serrated iron jaw trap on a chain. So the picture is: when a creature's bite lands, iron jaws spring shut on that
   creature. To make it the Seeker's gear rather than a generic bear trap:
   - it is a small hand-forged hunter's gin, about 0.4 of a creature's height, with the icon's round toothed ring;
   - it has no big ground plate;
   - its chain runs back to his belt.

   *Alternative B:* a braided cord snare, which suits the existing rope effect and the Form name SNARE. It contradicts
   the name, the icon, and the NET branch, which is the rope branch. See point 11.
2. **Armed: no world-space motif by default.** It would exist for 0 ms, because JAWS becomes ready on a bite and
   springs at once. Readiness lives in the dock instead: reactions get a real rearm sweep and the existing ready
   crossing, the ring and its quiet cue. A faint armed motif is allowed only for a build that keeps JAWS armed for at
   least 400 ms (a slow biter): the open jaws sit low and dim at the front creature's forefoot, material only, and
   fade in.
3. **Trigger: on the bite's contact frame.** This is the `EnemyStrike` time, which is also the row's contact frame.
   Core resolves JAWS and its answer at the same millisecond.
   - The jaws spring up from under the biter's forefoot.
   - They snap shut on its foreleg in about 60 ms: open, then shut, then a small overshoot.
   - The chain whips taut back to his belt on the same frame.
   - The reflected number and flash on that creature stay on that frame.
   - His own bite burst stays, because he was bitten.
4. **Retaliatory motion: along the bite's path.** Cause and answer read as one picture. The creature's lunge lands its
   forefoot on the trap at the moment its bite lands on him. The jaws clamp, and the taut chain yanks the creature a
   few pixels back into its row, a small eased recoil. The consequence lands on the one that pays. There is no
   row-wide ring and no burst anywhere else.
5. **Spent and rearm.**
   - The jaws hold for about 200 ms.
   - Then the chain slackens and the jaws drop open and fade over about 150 ms.
   - Nothing persists in the world.
   - The dock drains and refills over the rearm, and the ready moment is the dock's ready crossing.
   - There is no world cue at rearm; in practice the rearm coincides with the next bite.
6. **The champion's body does not participate.** He plays no clip, no cast and no lunge. The post-bite trap clip
   (laying a trap) is never played for JAWS. The chain's end is pinned to his drawn body at the belt, so it follows him
   through HARD HANDS' leap. The enemy caused this; JAWS is the answer.
7. **Attachment:** the chain runs from the champion's belt to the struck creature's forefoot. The struck creatures are
   the targets of the reflected `Strike` events, front to back. A second target (a wider shape) gets its own smaller
   jaws but no second chain.
8. **Audio: THUD, then SNAP.** The enemy's bite thud stays, because it is the cause.
   - Add one new cue on the same frame, `sfx_seeker_jaws_snap`: a spring-release tick, a hard iron clack and a short
     chain-rattle tail, about 180 ms long, panned to the creature.
   - Fallback: `sfx_seeker_jaws_snap`, then `sfx_hit` pitched up (today's reaction thud). The game has no generic trap
     sound.
   - Removed for JAWS: `sfx_cast` (this is not a cast), the generic reaction thud, and the generic reflected-hit thud
     and puff (impact priority).
   - The rearm is silent in the world.
9. **Colour roles:** the jaws and chain are MATERIAL: dark forged iron, untinted and alpha-blended, like SPRAY's steel.
   The Source is light only: a thin rim along the closing teeth, the snap flash, and a few sparks. Today the whole ring
   is tinted Shadow purple.
10. **Collision:** a reaction never owns the body.
    - It has its own layer: a reaction-performance list beside the action performance.
    - It overlaps SPRAY, HARD HANDS and swings.
    - It obeys the current duck, so it plays quieter under a performing action.
    - It keeps a skill-weight footprint: jaws about 0.4 of a creature's height, chain 2 px, 300 ms or less, no shake.
    - It drops the per-trigger callout, because the number already reads "−4 JAWS".
11. **Branches.** The variation names already choose the materials.
    - **NET** (Shadow) is rope: the jaws fling a cord mesh over the biter. MESH makes the mesh wider, SPITE makes it
      tighter, and RECOIL makes it rearm sooner. The verb is the same (reflect) with a new material.
    - **IRON** (Machine) changes the verb from bite back to CATCH. The jaws snap shut on the attacker's own jaws in
      front of him, before contact, and hold them. The champion's bite burst must give way, because the bite is
      stopped: its amount is 0 and it is counted as prevented. REPRISAL adds the answer, and PLATING adds the shield
      gain.
    - These branches are identified now and built after the base.
12. **A minimal reusable architecture.**
    - A `ReactionRecipe`, resolved through the accepted lookup at the SKILL tier only (`snare_jaws`). REPAY and every
      other trap keep their legacy presentation; there is no Form-level trap recipe.
    - A `ReactionPerformance`, spawned when the pump crosses the reaction's `Skill` event. It reads its targets from
      the answer's `Strike` events in the same batch. It draws the material and the light, voices the snap, and
      suppresses the generic cast effect, breath, thuds and puff for its own events.
    - An informational Core event carrying the rearm-ready moment, for the dock's reaction sweep. It must be
      gameplay-neutral and parity-tested.
    - `reaction` trace lines.
    - The legacy post-bite trap-clip path stays for recipe-less reactions.

**PixelLab, after approval only:** the forged jaws in a side view, open and shut, matched to the icon's silhouette
(1–2 generations, or drawn deterministically from the icon). The spring, the snap, the chain, the rim light, the
sparks and the fade are procedural at runtime, as the approved actions' trails and impacts are.

## Files

| File | What |
|---|---|
| `01a` / `01b` | the current trigger at true speed (normal TEMPO), with music / SFX only |
| `02` / `02b` | the same 4× slower; every frame |
| `03a` / `03b` | effects only (champion hidden), the idle trigger |
| `04a` / `04b` | the champion only (effects off): he never acknowledges it |
| `04c` / `04d` | the idle trigger every frame; at true speed |
| `05a` / `05b` | repeated triggers and the (invisible) rearm: normal TEMPO 16 s, fast TEMPO 5 s |
| `06a` / `06b` / `06c` | JAWS during HARD HANDS' leap (every frame; true speed) and during SPRAY's anticipation |
| `07a` / `07b` / `07c` | the dock tile armed / triggered / rearming / ready; the existing Seeker trap art; the JAWS and REPAY icons |
| `08_timelines.md` | every bite in every film, and one trigger in full |
