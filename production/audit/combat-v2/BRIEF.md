# COMBAT & CONTENT V2 — the owner's brief, verbatim

*Received 2026-09-01. Persisted unedited so it survives compaction. Section numbers are the owner's.*

---

This task is the next major **content-design and combat-mechanics rework** after the recent
architecture refactor. The architecture has already established the intended model:

`STYLE → SKILL → VARIATION → REINFORCEMENTS`

Now we need to improve the ACTUAL CONTENT built on top of that architecture. This task covers:
the twelve existing skills · all twenty-four Source variations · their reinforcements · Source
distribution · all six 2/3/4/5-piece Source sets · a real first-class SHIELD mechanic · Shield
presentation in combat · semantic combat events · run telemetry · save migration · liveness tests ·
balance tests · documentation and UI copy.

**This is NOT another architecture rewrite.** Preserve the current successful structural decisions
unless the live repository proves they have changed.

## 0. INSPECT CURRENT MAIN FIRST

Before implementing anything, inspect: SkillCatalogue · SkillProgress · ResolvedSkill / combat-effect
architecture if landed · SoloBattle / current battle simulation · set resolution · SkillShape /
BuildShape or equivalents · Source matchup · Vows · Mastery · character passives · Hunt presentation ·
replay/presentation events · Expedition Log telemetry · persistence and migrations · Build/Skill Tree
UI. Do not assume an older implementation because this prompt mentions an old class name.

## 1. PRESERVE THE CURRENT SKILL STRUCTURE

Keep: 6 Styles · 12 authored skills · 2 skills per Style · 1 Active + 1 Passive per Style · 2 Active
slots · 2 Passive slots · exactly 2 authored variations per skill · Source belonging to the variation ·
3 reinforcements under the chosen variation · skill progression through use · skill levels retained
permanently · free skill respec.

Four-level progression stays: Level 1 → choose variation; Levels 2–4 → three reinforcements. Do not
expand each skill into a giant talent tree. **Depth should come from interactions, not node count.**

## 2. CONTENT DESIGN LAW

BASE SKILL defines the combat verb. VARIATION changes HOW that verb solves a problem. SOURCE explains
WHY that variation behaves that way. REINFORCEMENT 1 deepens the variation's primary mechanic.
REINFORCEMENT 2 creates a useful build interaction. REINFORCEMENT 3 creates a small behavioural/rule
change. A reinforcement should usually do more than `+X% damage`. Numerical reinforcement is allowed
when it supports the branch identity, but every branch should contain meaningful behaviour.

## 3. A VARIATION MUST KEEP ITS TRADEOFF

A fully reinforced variation must still feel like the variation the player originally selected. Do NOT
author "extremely strong but lasts only 2 seconds" and then let a reinforcement remove the downside.
The branch may soften a downside. It should not erase the branch's reason to exist.

## 4. STYLE OWNERSHIP REMAINS STRICT

- **HAMMER** — one huge impact. Owns: heavy hit, defence ignore, defence break, stun, execute, overkill.
- **VOLLEY** — hit count and target economy. Owns: repeated hits, arrow/hit count, spread, target
  coverage, bleed through repeated attacks, kill-driven volume.
- **SNARE** — enemy aggression works in your favour. Owns: retaliation, reflect, damage taken, reactive
  protection, shield from retaliation.
- **SIGN** — amplification. Owns: amplification magnitude, duration, charges, uptime, target
  amplification. SIGN itself does not become a damage Style.
- **FIELD** — persistent whole-wave influence. Owns: area damage, persistent damage, slow, living-enemy
  scaling, persistent pressure.
- **DRAIN** — combat returns something to the Hunter. Owns: lifesteal, healing, health scaling, attack
  break, sustain.

Do not casually give one Style another Style's defining verb.

## 5. SOURCE DISTRIBUTION MUST BE BALANCED

**EVERY SOURCE MUST APPEAR ON EXACTLY 2 Active variations and 2 Passive variations.** Target matrix:

| Source  | Active         | Active            | Passive          | Passive        |
| ------- | -------------- | ----------------- | ---------------- | -------------- |
| BODY    | BLOW / FLATTEN | SPRAY / CLUSTER   | PRESS / CRUSHING | WILT / SUP     |
| MACHINE | REPAY / BANKED | DRINK / SIPHON    | PRESS / PIN      | JAWS / IRON    |
| MIND    | CALL / SPEND   | SPRAY / SPLAY     | BRAND / SPRAWL   | WILT / SHRIVEL |
| NATURE  | PULSE / THRONG | DRINK / GLUT      | WEEP / TORRENT   | MIRE / NUMB    |
| SHADOW  | BLOW / FINISH  | REPAY / VENGEANCE | JAWS / NET       | WEEP / CARRION |
| SPIRIT  | CALL / STEADY  | PULSE / SHARE     | BRAND / ETCH     | MIRE / TEEMING |

It makes Single-Source Vows and mono-Source builds equally possible for all six Sources.

## 6. SOURCE FANTASIES

**BODY** physical force · flesh · durability · direct impact · physical persistence · concentrated
force. **MACHINE** armour · construction · shielding · precise mechanisms · prevention · controlled
systems. **MIND** precision · manipulation · selection · certainty · calculation · amplification/control.
**NATURE** growth · regeneration · propagation · accumulation · living systems · gradual deepening.
**SHADOW** death · retaliation · execution · sacrifice · aftermath · lingering harm. **SPIRIT** cycles ·
resonance · persistence · redistribution · accumulation through repetition · harmony.

## 7–26. SHIELD

**7.** Shield must become a REAL combat mechanic — not a UI label, a SkillDef boolean, temporary fake
HP, or a visual-only effect. Implement end-to-end.

**8. Core semantics.** The Hunter has `CurrentShield`. Shield is a temporary combat resource that
absorbs incoming damage before Health. Shield is NOT Health. It must never change the meaning of:
current HP · maximum HP · low-health conditions · full-health conditions · health scaling · death
thresholds.

**9. Cap.** One global capacity: **Max Shield = 50% of Max Health.** All gains stack additively to the
cap. Do not create separate Machine/Nature/Snare/Barrier/Ward shield pools. One readable resource.

**10. Wave-local.** **Shield resets to 0 at the start of every wave.** After reset, wave-start effects
may grant Shield. Do NOT persist ordinary Shield across waves. Do not introduce persistent cross-wave
Shield in this rework.

**11. Damage order.**
```
Enemy attack → candidate final damage (attack break, mitigation, flat reduction, other normal
mitigation) → hard-prevention checks → Shield absorbs remaining → remainder damages Health
```
The exact implementation fits the current pipeline; the semantic requirement is what matters.

**12. Hard prevention happens BEFORE shield consumption.** JAWS/IRON, MACHINE 5p, other genuine "this
attack deals nothing" mechanics. If an attack is already fully prevented, Shield must NOT be consumed —
and MACHINE 5p must not waste its once-per-wave prevention on a bite IRON already stopped.

**13. Shield absorbs POST-MITIGATION damage.** Raw 100 → mitigated 70 → Shield 50 absorbs 50, Health
loses 20. Do not subtract raw 100 from Shield.

**14. Shield does not count as health damage taken.** Critical for SNARE: a fully absorbed 100-damage
bite means `HealthDamageTaken = 0`; REPAY must not bank it. Nor may absorption count toward Health Lost
per Wave, low-health triggers, damage-taken banks or health-loss quests. Track separately: damage
attempted · damage prevented · shield absorbed · health damage taken.

**15. A shielded bite is still a bite.** `OnBitten` mechanics may still react; `HealthDamageTaken`
mechanics do not unless Health actually falls. Semantic stages: `BiteStarted → Reaction/Prevention →
DamageResolved (ShieldAbsorbed, HealthDamage)`.

**16. Healing does not restore Shield.** Only an explicit Shield-producing rule may produce Shield:
REPAY/BANKED · JAWS/IRON/PLATING · MACHINE set · NATURE 5p Overgrowth.

**17. Shield and overheal.** NATURE 5p explicitly allows eligible overhealing to become Shield: apply
normal healing rules → respect the per-wave healing budget → heal missing Health → determine the
eligible healing that would otherwise be wasted → convert only the specified fraction → respect the 50%
cap. Do not bypass the healing ceiling. Do not let Shield generation recursively count as healing.

**18. Events.** Core publishes semantic events/state: `ShieldGained` · `ShieldAbsorbed` ·
`ShieldBroken` · `ShieldExpired`, carrying useful gameplay values (amount gained, amount absorbed,
shield remaining, capacity, cause). No animation commands from Core.

**19. Shield is also persistent battle state.** The battle state/snapshot used by presentation must
expose `CurrentShield` and `MaxShield`. Reconstructing the view mid-fight must show Shield without
having witnessed the original event. STATE is state; EVENT is event.

**20. Headless/offline.** Shield must work identically in live simulation, fast-forward, offline
simulation and deterministic tests. No dependency on MonoGame, animation, VFX, frame rate or UI.

**21. Hunt HUD.** Add Shield presentation. HP bar remains primary; a visually distinct Shield bar
immediately adjacent (or a thinner Shield layer immediately above Health). Do not hide Health behind an
overlay. 400 HP + 0 Shield must be distinguishable at a glance from 400 HP + 200 Shield.

**22. Visual language.** Not the red of Health. Pale steel / muted cyan / cold silver — a distinct
defensive value, readable on the dark UI. Do not rely on colour alone: shield glyph, distinct bar
geometry, shield VFX.

**23. Combat VFX.** While `CurrentShield > 0` show a subtle persistent defensive visual (faint barrier
rim, small outline, restrained shell) that does not cover the character art or create constant noise.
On gain: a brief build-up. On absorption: a small impact on the barrier. On break: a clearer
crack/burst. Reuse the existing persistent-state VFX infrastructure.

**24. Floating feedback.** No combat-number spam. Gain may show `+42 SHIELD`. Absorption is
communicated mainly through the bar and the barrier impact. If a bite splits between Shield and Health,
the Health damage remains the primary floating number.

**25. Expedition Log telemetry.** Add `SHIELD ABSORBED` when meaningful, beside `HEALTH LOST`. Do not
rename existing armour/mitigation metrics — they answer different questions. Do not show a zero-value
Shield row when the build had no Shield mechanic.

**26. UI outside Hunt.** One canonical player-facing word: **SHIELD**, one glyph, everywhere. Flavour
names such as PLATING may name abilities/capstones; the resource stays Shield.

## 27–48. SKILL REWORK

**27. HAMMER — ONE HIT SHOULD MATTER.**
*BLOW (Active)*: heavy single-target hit, 6 beats, keep identity; not a generic multi-target attack.
*BLOW / FLATTEN — BODY*: "raw physical force ignores defence"; base ignores target Defence.
  - **TOLL** ~+40% damage (benchmark).
  - **BREAKTHROUGH** 50% of BLOW's direct overkill carries into the next living enemy (shared overkill
    semantic). Replaces the old "hit a second target".
  - **TRAIL** after BLOW resolves the NEXT basic attack ignores Defence — a one-use state, not a
    permanent grant.
*BLOW / FINISH — SHADOW* (was MACHINE): execute under ~15% Health, once per wave.
  - **BRINK** threshold ~25%. **TWICE** may execute twice per wave (~20% threshold if needed).
  - **CLEAN CUT** on a successful execute, unused direct-hit overkill from that BLOW carries to the next
    living enemy. Not cooldown reduction. The branch stays about execution, excess damage, death.

**28. PRESS (Passive HAMMER)** — persistent weight breaks the front enemy's Defence over time.
*PRESS / CRUSHING — BODY*: keep structure — **SETTLE** deeper floor · **SEIZE** two front enemies ·
**UNDERMINE** more frequent.
*PRESS / PIN — MACHINE*: periodically stun instead of continuous crushing.
  - **HOLD** longer stun. **BUCKLE** stun also applies Defence Break. **INTERCEPT** PIN prioritises the
    living enemy whose next attack is due soonest (deterministic timing; no random targeting).

**29. SNARE — THE ENEMY SHOULD REGRET HITTING YOU.** REPAY repays a multiple of *actual Health damage*
taken since its previous cast. Shield absorption does NOT feed REPAY.
*REPAY / VENGEANCE — SHADOW*: counts only recent Health damage (~3s) but pays much harder (~350%).
  - **GRUDGE** ~500%. **RAW NERVE** window 3s → 2s but substantially higher rate. **SCARRED** below a
    meaningful Health threshold (~40%) increase payback — evaluated on Health, never Shield.

**30. REPAY / BANKED — MACHINE.** A main Shield skill: REPAY grants Shield based on banked Health damage
instead of dealing it. Respects the global cap.
  - **LINING** bank 200% → 300%. **STANDING** Shield ~50% larger (before the cap clamp). **CARRIED**
    start every wave with Shield ≈ **10% Max Health** while BANKED is equipped — not carried-over Shield.

**31. SNARE — JAWS.** Being bitten retaliates.
*JAWS / NET — SHADOW*: ~100% reflect. **MESH** reflect grows per bite this wave, bounded · **SPITE**
higher base reflect · **RECOIL** rearms faster.
*JAWS / IRON — MACHINE*: periodically stop an entire bite; longer rearm. **BLUNT** faster rearm ·
**REPRISAL** a stopped bite is also reflected in full · **PLATING** a stopped bite grants Shield equal to
~50% of its post-mitigation would-be damage (respect cap).

**32. SIGN DEALS NO DAMAGE ITSELF; IT MAKES OTHER THINGS STRONGER.**
*CALL / SPEND — MIND*: away from a tiny timing lottery — CALL empowers a limited number of damaging hits.
Suggested base: next 3 direct damaging hits within ~6s, each greatly amplified.
  - **OVERSPEND** more amplification per hit. **COUNT** 3 → 5 hits. **PERFECT CLAUSE** a Critical hit
    receives the amplification but does not consume a charge (deterministic; no infinite loop).

**33. CALL / STEADY — SPIRIT**: each CALL adds persistent amplification for the rest of the wave, to a
cap. **REDOUBLE** larger gain · **PILLAR** higher cap · **FOUNDATION** begin each wave with one stack.
Not cooldown reduction.

**34. BRAND / SPRAWL — MIND**: spread weaker amplification across the whole wave. **EVEN** greater
whole-wave magnitude · **ANCHOR** front enemy takes full-strength BRAND while the rest take SPRAWL ·
**WINNOW** SPRAWL deepens over time, bounded.

**35. BRAND / ETCH — SPIRIT**: the focused BRAND deepens repeatedly and remembers its depth when it
moves. **SINK** faster per tick · **GRAVEN** higher max · **PACE** more frequent. Do not rewrite
unnecessarily if already live and tested.

**36. VOLLEY — MANY HITS. TARGET ECONOMY.** SPRAY fires five repeated hits. Use real repeated-hit
semantics.
*SPRAY / SPLAY — MIND*: distribute arrows across as many living enemies as possible, then prefer
deterministic useful targets (e.g. lowest health). No uncontrolled random targeting.
  - **EXTRA STRING** ~+2 arrows. **NOCK** each arrow ~+25%. **CLEANUP** if a target dies before all
    arrows resolve, remaining arrows retarget another living enemy. No chain recursion.

**37. SPRAY / CLUSTER — BODY** (was SHADOW): all five hits concentrate on one enemy. **DRIVE**
substantially more total damage · **RUPTURE** repeated hits leave Bleed based on damage dealt ·
**PUNCH THROUGH** if the target dies early, remaining arrows continue into the next living enemy.
Remove "also hit a second enemy" — it undermines CLUSTER's identity.

**38. WEEP / TORRENT — NATURE** (was MIND): bleed grows and propagates; base pays out faster. **DRY**
more kill-generated Bleed · **SPILLWAY** even faster payout · **FLOOD** while WEEP Bleed is active,
further kills add extra Bleed beyond the ordinary contribution (bounded; no exponential self-growth).

**39. WEEP / CARRION — SHADOW**: death leaves something behind; keep the cross-wave lingering Bleed if
deterministic and healthy. **DREGS** more kill Bleed · **ONSET** ordinary repeated-hit skill damage also
contributes a small carried Bleed · **LAST DROP** carried Bleed pays out faster. Prevent unbounded growth
across idle waves.

**40. FIELD — THE WHOLE WAVE MATTERS.** PULSE: direct area hit against the whole wave.
*PULSE / THRONG — NATURE*: damage rises with living enemy count. **HORDE** higher per-enemy scaling ·
**PACKED** higher base · **CROWDED** faster ONLY while genuinely crowded (e.g. 4+ living). No
unconditional cooldown reduction.

**41. PULSE / SHARE — SPIRIT**: one large pool distributed among living enemies. **POOL** bigger pool ·
**NARROWED** at most two targets · **BALANCE** health-aware distribution preferring higher-current-health
enemies to reduce overkill waste. Deterministic; a simple stable weighting is enough.

**42. MIRE / NUMB — NATURE**: slow deepens over time. **DEEPEN** faster growth · **SEDIMENT** higher
ceiling · **SILT** more persistent damage. No major redesign needed.

**43. MIRE / TEEMING — SPIRIT**: slow scales with enemies present. **CLOG** more slow per enemy ·
**BRIM** higher start/ceiling · **REMNANT** enemies that died during the CURRENT wave keep contributing
to the count until the wave ends (never across waves). Benchmark the ceiling; no permanent shutdown.

**44. DRAIN — COMBAT RETURNS SOMETHING TO YOU.** DRINK: heavy single-target damage and lifesteal.
*DRINK / SIPHON — MACHINE* (renamed from THIRST; requires save migration): a mechanical pump makes
lifesteal reliable. **PUMP** substantially more lifesteal · **PARCH** more DRINK damage · **TRICKLE**
basic attacks gain a small bounded lifesteal.

**45. DRINK / GLUT — NATURE**: no lifesteal; damage rises with current Health. Shield does NOT count as
Health. **SURFEIT** more Health scaling · **STOUT** higher base · **RIPE** above ~90% actual Health the
scaling ceiling rises further. No cooldown reduction.

**46/47. WILT / SUP — BODY** (was SHADOW): pulses also heal ~1% Max Health. **BROOK** deeper Attack
Break floor · **BALM** more heal per pulse (1% → 1.5%) · **RESERVE** more Attack Break per pulse. Avoid a
reinforcement that doubles tick frequency and every output at once unless benchmarked.

**48. WILT / SHRIVEL — MIND** (was NATURE): precisely cripple the front threat. **HOLLOW** also reach the
second enemy at reduced strength · **SEIZED** greater break per pulse · **GAUNT** much deeper floor.

## 49–62. SETS

**49.** Remove set-Source damage matching completely (the 2p/4p `+8% matching-Source damage` model). Do
not replace it with a bigger percentage. The set's Source describes the EQUIPMENT PHILOSOPHY.

**50. SET SOURCE != REQUIRED SKILL SOURCE.** A BODY skill build may wear SHADOW gear. Source matching
still matters through variations, matchup, Vows and other real Source systems.

**51.** Keep 2/3/4/5-piece structure across eight worn slots, enabling 5+3, 4+4, 3+3+2. 5p should be the
most identity-defining, not necessarily the highest DPS.

**52. BODY — MOMENTUM** (physical momentum, health, basic swing, excess force).
2p +10% Maximum Health · 3p basic attacks ~+20% damage · 4p ~35% of direct-hit overkill carries to the
next living enemy (direct-hit semantics; periodic damage must not feed it) · **5p MOMENTUM** after an
Active resolves, the next basic attack becomes **IMPACT**: ~+75% damage and carries 100% of its overkill.
Only one IMPACT pending at a time.

**53. MACHINE — PLATING** (prevent, shield, stay intact). Introduces Shield even with no Shield skill.
2p small flat reduction per bite (~4, benchmarked; use a bounded formula if flat becomes irrelevant) ·
3p at wave start gain Shield ≈ **12% Max Health** (after the reset) · 4p while `CurrentShield > 0` take
~**10% less** incoming damage (before absorption; no double-counting) · **5p PLATING** once per wave the
first eligible bite that would deal positive damage is completely prevented, then grants Shield equal to
**100% of that bite's post-mitigation would-be damage** (respect cap). If another effect already fully
prevents the bite, PLATING is NOT consumed and no Shield is created.

**54. MIND — CERTAINTY** (precision, reduce randomness, eventual certainty; direct damaging hits only).
2p +5pp Critical Chance · 3p ~+20pp Critical Damage · **4p FOCUS** each direct damaging hit that does NOT
crit grants +3pp temporary Crit Chance, stacking to ~+15pp; a Crit resets FOCUS; reset at wave start ·
**5p CERTAINTY** at the FOCUS cap the next eligible direct damaging hit is guaranteed to Crit, after
which FOCUS resets. No recursive guaranteed-Crit loop.

**55. NATURE — OVERGROWTH** (recover, grow, waste nothing).
2p ~0.3% Max Health per second in combat · 3p combat healing ~20% stronger (legitimate healing only; do
not multiply Shield) · 4p per-wave healing ceiling ~+50% · **5p OVERGROWTH** when eligible combat healing
would be wasted at full Health, convert **50% of that eligible overheal into Shield** — cap applies,
Shield is not healing, no recursion, ceiling still applies, Health-based mechanics read Health only.

**56. SHADOW — AFTERIMAGE** (death prepares the next death).
2p ~+12% damage against enemies below 30% Health · **3p SHADE** a kill grants 1 SHADE; the next DAMAGING
Active consumes one and deals ~+25% direct damage (non-damaging Actives do not consume); a Shade may
persist between waves · 4p maximum stored Shades **2**, one consumed per eligible Active ·
**5p AFTERIMAGE** when an Active consumes a Shade, after it resolves create an AFTERIMAGE dealing ~50% of
that Active's resolved DIRECT damage to the same semantic target set. It costs no beat, starts no
cooldown, is not another cast, is not skill-progression use, consumes no Shade, creates no Shade from its
kills, and cannot recurse. Use an explicit damage origin/tag. Do NOT rerun the skill's stateful logic
(REPAY must not spend its bank twice).

**57. SPIRIT — HARMONY** (cycle, resonance, all skills work together).
2p ~+6% Skill Rate · 3p the FIRST activation of each equipped skill each wave gets ~**+20% EFFECT
MAGNITUDE** — defined narrowly: direct damage ×, direct heal ×, Shield grant ×, only the BONUS portion of
an Amplify; NOT stun duration, slow caps or Defence-Break floors. Implement only the typed effects that
need it; no universal property bag.

**58. SPIRIT 4p — RESONANCE**: the first time each DISTINCT equipped skill activates in a wave, gain a
small Skill Rate bonus for the rest of the wave (~+2% each, ~+8% cap), once per skill per wave.

**59. SPIRIT 5p — HARMONY**: requires ALL FOUR SKILL SLOTS FILLED (a natural conflict with an
empty-slot Vow; no special branch). When all four equipped skills have activated at least once in the
wave, grant each skill one HARMONY CHARGE: its next activation receives the 3p +20% Effect Magnitude one
additional time. At most one charge per skill per wave. No cooldown resets, no fake casts.

**60. Set interaction law.** Sets must not become six independent combat engines. Reuse shared semantics
(overkill, Shield, Crit, healing, kill, Active skill, first activation, Skill Rate). Add typed state only
when genuinely required: CurrentShield · MindFocus · ShadeCount · BodyImpactPending · Spirit activated
mask / Harmony charges. Do not build a generic buff scripting runtime.

**61. Source sets must be self-contained** — a 5p should make sense with no matching-Source skill.

**62. Balance the set shapes.** Benchmark 5+3, 4+4, 3+3+2 across several real builds. No structure should
universally dominate.

## 63–68. VALIDATION TARGETS

**63. Mono-Source builds** (must compose, satisfy Single-Source requirements, run headless, produce
meaningful output, contain no impossible slot combination):
BODY FLATTEN/CLUSTER/CRUSHING/SUP · MACHINE BANKED/SIPHON/PIN/IRON · MIND SPEND/SPLAY/SPRAWL/SHRIVEL ·
NATURE THRONG/GLUT/TORRENT/NUMB · SHADOW FINISH/VENGEANCE/NET/CARRION · SPIRIT STEADY/SHARE/ETCH/TEEMING.

**64. Mixed builds**: BANKED + MACHINE 5p · GLUT + NATURE 5p · VENGEANCE + MACHINE Shield · SPEND + MIND
set · CLUSTER + MIND set · FINISH + SHADOW set · DRAIN + NATURE 5p · SPIRIT 5p + empty-slot Vow. Do not
hardcode special-case pairings when general semantics already produce the result.

**65. Reinforcement liveness.** A reinforcement is live only if, in the same battle/skill/variation/
build/seed, WITH it produces an observable intended difference against WITHOUT it. Conditional
reinforcements must be tested under a satisfying scenario. Do not call a reinforcement implemented
because a field changed in memory — prove the battle consumes it.

**66. Variation balance.** The two fully reinforced variations of each skill should be competitive at
different problems. The question is "When would I choose this?" — both branches need a real answer.

**67. Active-skill beat budget.** Two Actives must not erase the basic attack. Audit cadence changes; no
unconditional reinforcement may casually exceed the current safe budget. BODY 5p depends on basic swings.

**68. Shield balance.** Shield must not become "Health but better". Benchmark: no Shield · BANKED ·
IRON/PLATING · MACHINE 3p · MACHINE 5p · NATURE 5p · combinations. Limits: 50% cap · wave-local · no
cross-wave persistence · Overgrowth constrained · blocked attacks do not feed REPAY · Shield is not full
Health for health-scaling.

## 69–72. TELEMETRY AND UI

**69.** Extend telemetry to distinguish damage dealt · damage prevented · Shield absorbed · Health damage
taken · healing · overheal converted · kills · crit behaviour · overkill. Telemetry supports testing,
diagnostics, the Expedition Log and balance tools; the combat screen stays readable.

**70. Skill tree UI** must show the new Sources — especially FINISH→SHADOW, CLUSTER→BODY, TORRENT→NATURE,
SIPHON→MACHINE, SUP→BODY, SHRIVEL→MIND. Update glyph, colour, branch text, tooltips, reinforcement
descriptions. No stale Source association may remain.

**71. Shield skills UI** use one canonical glyph and term, with a tooltip: `SHIELD absorbs incoming
damage before HEALTH.` A contextual glossary is enough after first introduction.

**72. Set ladder UI** — active tiers visually distinct from inactive; 5p capstone names emphasised
(MOMENTUM · PLATING · CERTAINTY · OVERGROWTH · AFTERIMAGE · HARMONY).

## 73–78. PERSISTENCE

**73.** Skill uses/levels are permanent. When removing/renaming reinforcements, preserve Uses/Level; if
an old reinforcement has no clean equivalent, remove the purchase and return the level as unspent.

**74.** `DRINK / THIRST` → `DRINK / SIPHON` with explicit migration; map old reinforcement names where
semantically honest, otherwise preserve the level as unspent.

**75.** A Source change alone must not force a respec: FINISH, CLUSTER, TORRENT, SUP, SHRIVEL keep their
selection. Re-evaluate Vow validity normally after load and show honest current validity; do not silently
mutate unrelated choices.

**76.** Set state derives from worn item Source. Do not mutate item Sources. No item disappears.

**77.** Shield is wave-local runtime state — do not add it to long-term saves unless the game persists
mid-wave battle state. Audit rather than assume.

**78.** Remove dead legacy fields after migration (old `ShieldInsteadOfDamage`, set-only SourceBonus,
removed reinforcement dials, stale MARK terminology, old cooldown-specific reinforcement state) when no
live consumer remains. Keep anything with a legitimate live consumer.

## 79–82. ARCHITECTURE

**79.** Do not reintroduce the old giant SkillDef. If the repo is partially migrated, continue toward the
typed semantic architecture — but do not build a universal scripting engine.

**80. Small typed states are acceptable** where real content consumes them: Shield · REPAY bank · SPEND
charges · STEADY stacks · Mind Focus · Shades · Body Impact pending · Harmony mask/charges · per-wave
execute count. Each needs a clear owner, reset rule, consumer and tests. Not `Dictionary<string,object>`.

**81. Reset rules must be explicit.** Shield: wave start. Mind Focus: wave start. Body Impact: consumed by
next basic attack, else wave end. Shades: may persist between waves, bounded 1/2. Spirit distinct-skill
activation: wave start. Harmony charges: wave start. Execute count: wave start. STEADY: wave start, then
FOUNDATION may add one. No hidden state lifetime.

**82. Loop safety.** Test against recursion: AFTERIMAGE must not trigger AFTERIMAGE · Overgrowth Shield is
not healing · Afterimage kills create no Shade · Bleed must not grow exponentially · overkill carry must
not recurse unbounded · Shield-prevented damage must not feed REPAY. A good build may create a loop; it
must not create an unintended infinite one.

## 83–90. BENCHMARKS AND TESTS

**83.** Use the repository's real encounter archetypes/bands: one durable enemy · many-enemy Swarm · high
armour · high incoming damage/Bruiser · caster/tempo pressure · long wave · short wave. Do not invent a
parallel benchmark model.

**84.** Measure beyond DPS: depth reached · average wave time · health remaining · Shield absorbed ·
healing · damage · kills · overkill efficiency · crit consistency.

**85. Documentation** must state one current truth: the new Source matrix, all 12 skills, 24 variations,
reinforcements, Shield (reset/cap/order), the six ladders, set independence from skill Source, migration
rules. Remove stale claims (2p/4p matching-Source +8%, FINISH MACHINE, CLUSTER SHADOW, TORRENT MIND,
THIRST SHADOW, SUP SHADOW, SHRIVEL NATURE).

**86. Shield tests** (20 listed): absorbs before Health · post-mitigation · partial · full · 50% cap ·
per-wave reset · wave-start grant after reset · does not feed REPAY · not Health for GLUT · does not
satisfy low/full-health checks · healing does not restore it · Overgrowth converts only eligible overheal ·
Overgrowth respects the ceiling · hard-prevented bite consumes no Shield · IRON does not waste MACHINE 5p ·
MACHINE 5p does not trigger on an already-stopped attack · PLATING gain capped · offline == live · snapshot
exposes Shield · ShieldBroken fires exactly at the zero crossing.

**87. Set tests** — each tier independently; mixed 5+3, 4+4, 3+3+2; the six loops; every tier has a real
downstream consumer.

**88. Source distribution invariant** — per Source exactly 4 variations, 2 Active, 2 Passive; every skill
exactly 2 variations and 3 reinforcements each.

**89. Save migration tests** with old-save fixtures: THIRST→SIPHON · Uses preserved · invalid purchases
become free levels · variation selections survive Source reassignment · gear Sources preserved · sets
resolve new bonuses · no corruption · save and reload in current format.

**90. Presentation tests** where practical: Shield bar appears/disappears · state reconstructs without
historical events · gain/absorb/break VFX map · skill cards use correct Source · set ladder copy · no
stale Source icons. No MonoGame tests in Core.

## 91. FINAL DESIGN LAWS

1. Every Source can build 2 Active + 2 Passive.
2. A variation remains meaningfully different even when fully reinforced.
3. A Style owns its own combat verbs.
4. Shield is not Health.
5. Prevented damage is not damage taken.
6. Set Source does not require matching Skill Source.
7. 2p may be simple; 5p must change how the build behaves.
8. No set capstone may recursively trigger itself.
9. No reinforcement exists without a measurable consumer.
10. Do not solve balance by stacking generic damage multipliers.
11. Auto-battler mechanics should be predictable enough to build around.
12. Tradeoffs survive progression.

## 92. FINAL DELIVERABLE

Report on: **Skills** (1 final catalogue · 2 variations · 3 Source matrix · 4 reinforcements · 5 changed
mechanics · 6 changed Sources · 7 removed concepts · 8 numerical tuning) · **Shield** (9 state model ·
10 capacity/reset · 11 resolution order · 12 events · 13 telemetry · 14 HUD · 15 VFX · 16 offline/replay) ·
**Sets** (17–22 the six ladders) · **Architecture** (23 new typed state · 24 removed fields · 25
abstractions deliberately not introduced) · **Persistence** (26 migrations · 27 THIRST→SIPHON · 28 fixture
results) · **Validation** (29 liveness · 30 mono-Source · 31 mixed · 32 set-shape comparison · 33 Shield
balance · 34 suite) · **Remaining** (35 values needing playtest · 36 deferred debt).

## 93. FINAL INSTRUCTION

Do not optimise for preserving today's numbers, nor for adding as many mechanics as possible. Optimise for
readable build identity · meaningful variation choices · strong Source fantasies · real interaction between
systems · deterministic idle combat · predictable automation · long-term skill expansion · interesting set
commitments · visible defensive gameplay · no fake choices.

The target is a player saying:

> "I run MACHINE BANKED and IRON, so enemy pressure becomes Shield. My MACHINE set starts each wave
> protected and converts the first dangerous bite into more Shield."

> "My SHADOW FINISH and VENGEANCE build stores Shades from kills, then my next heavy Active leaves an
> Afterimage."

> "My NATURE build stays near full Health, converts wasted healing into Shield, and GLUT rewards me for
> staying healthy."

> "My SPIRIT build wants all four skills cycling because HARMONY rewards completing the full rotation."

They should describe mechanics and relationships. Not a list of percentage bonuses.
