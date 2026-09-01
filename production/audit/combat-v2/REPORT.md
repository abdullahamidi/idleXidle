# COMBAT & CONTENT V2 — final report

*Answering the brief's §92, in its order. Every claim here is measured; the tests that measure it are
named beside it.*

---

## SKILLS

### 1. The final catalogue

Twelve skills, unchanged in identity and cadence: **BLOW · PRESS** (HAMMER) · **REPAY · JAWS** (SNARE) ·
**CALL · BRAND** (SIGN) · **SPRAY · WEEP** (VOLLEY) · **PULSE · MIRE** (FIELD) · **DRINK · WILT** (DRAIN).
One Active and one Passive per style; two Active and two Passive slots. The catalogue is generated into
`design/gdd/skill-slots-and-skill-trees.md` and gated by `tools/check_skill_doc.py`, so the design doc
can no longer disagree with the fight.

### 2. Variations

Twenty-four, two per skill, each a `Func<SkillDef,SkillDef>` delta so a variation composes with its
reinforcements by construction rather than by discipline.

### 3. The Source matrix

**Every Source appears on exactly two Active and two Passive variations.** Written as an invariant test
*before* the content, so it constrained the rewrite rather than describing it (`source_matrix_test.cs`).

| | Active | Active | Passive | Passive |
|---|---|---|---|---|
| **BODY** | BLOW / FLATTEN | SPRAY / CLUSTER | PRESS / CRUSHING | WILT / SUP |
| **MACHINE** | REPAY / BANKED | DRINK / SIPHON | PRESS / PIN | JAWS / IRON |
| **MIND** | CALL / SPEND | SPRAY / SPLAY | BRAND / SPRAWL | WILT / SHRIVEL |
| **NATURE** | PULSE / THRONG | DRINK / GLUT | WEEP / TORRENT | MIRE / NUMB |
| **SHADOW** | BLOW / FINISH | REPAY / VENGEANCE | JAWS / NET | WEEP / CARRION |
| **SPIRIT** | CALL / STEADY | PULSE / SHARE | BRAND / ETCH | MIRE / TEEMING |

Each row composes into a whole hunter that fights in all four bands — proved, not asserted
(`test_a_source_can_field_a_whole_hunter_out_of_its_own_variations`).

### 4. Reinforcements

Seventy-two, three per variation, **every one measured live at four depths** against the same skill on
the same variation without it (`reinforcement_liveness_test.cs`, 202 cases).

### 5. Changed mechanics

Fifteen typed rules on one `SkillRules` group — not thirty loose booleans on `SkillDef`, and not a
`Dictionary<string, object>`. New: skill-owned overkill carry · a one-use trail on the next swing · a
hit-counted amplify window · a deeper mark on the front enemy · payback that scales with how hurt you
are · bleed that compounds on a wave already bleeding · health-scaling that reaches further near full ·
a primed opening stack · a stun held for the bite · a health-aware split · retargeting · a finisher
bonus · weakening per enemy lost · a widened healing ceiling.

### 6. Changed Sources

Eleven variations moved to satisfy the matrix. **None costs a respec**: they kept their names, and
`SkillProgress.Restore` matches by name (`save_migration_test.cs`). The six the brief names —
FINISH→SHADOW, CLUSTER→BODY, TORRENT→NATURE, SIPHON→MACHINE, SUP→BODY, SHRIVEL→MIND — are pinned one by
one, because the skill tree draws its glyph, colour and branch text from `variation.Source` and a stale
association there is a stale association on every screen at once.

### 7. Removed concepts

The matching-Source set bonus · four `SkillShape` dials with no producer (`SourceBonus`, `Leech`,
`FirstBiteFree`, `CooldownRefundOnKillMs`) · `WaveReplay`'s UNDYING-fed "IsShielded" · twenty-five
reinforcements replaced outright.

### 8. Numerical tuning

Deliberately light. Three numbers moved, and each because a **measurement** said so, not because a
number looked wrong: TOLL 50%→40% (benchmark), SHRIVEL 20%/−80% → 25%/−90% (it led nowhere), SPLAY's
surplus arrows became hits rather than a multiplier (it was beating CLUSTER at concentrating).

---

## SHIELD

### 9. The state model

`Champion.CurrentShield` — one float, wave-local, never saved. `MaxShield` derived. Every producer goes
through one `GrantShield` door so the cap is enforced once and the event reports what was *actually*
added.

### 10. Capacity and reset

**50% of maximum health**, reset to zero at the start of every wave, wave-start grants applied after the
reset. No carry between waves, none between runs.

### 11. Resolution order

Mitigation → prevention → absorption → health. Prevention is decided **before** the pool or the shield is
touched, which is what lets a second preventer see the bite is already gone and keep its charge. Full
rules, edge cases and formulas: `design/gdd/shield.md`.

### 12. Events

`ShieldGained` · `ShieldAbsorbed` · `ShieldBroken`. The old single `Shield` kind carried a banked-health
figure for one producer and a duration for another — one payload, two meanings.

### 13. Telemetry

`WaveMetrics.ShieldAbsorbed` and `DamagePrevented`; `RunReport.ShieldAbsorbedFraction` and
`ShieldAbsorbedPerWaveFraction`, through `RunLog` and the save. `AbsorbedFraction` is untouched — that
one is armour eating the champion's *outgoing* damage and answers a different question.

### 14. HUD

A thin steel strip immediately above the pool, never over it. Half the height, its own hard edge,
vertical scoring, the word and figure beside it — three encodings, so it does not rely on colour.
Present from the first grant of a run onward.

### 15. VFX

A held barrier for as long as the shield stands (the same `VfxPlayer.Hold` the aura uses), restrained
and slow. `+42 SHIELD` on gain; nothing on absorb; a loud break.

### 16. Offline and replay

`WaveReplay` keeps the running total from the events, so a screen opened mid-wave draws the right bar
without having seen the grant. Offline simulation is the same `SoloBattle`, so shield behaves identically
away from the screen.

---

## SETS

### 17–22. The six ladders

| Set | 2p | 3p | 4p | 5p |
|---|---|---|---|---|
| **BODY — MOMENTUM** | +10% health | swing +20% | 35% of a direct hit's overkill carries | after a skill the next swing is an IMPACT: +75% and it carries all its waste |
| **MACHINE — PLATING** | −4 a bite | wave opens with 12% health as shield | −10% while shielded | once a wave the first bite that would hurt is stopped and becomes shield |
| **MIND — CERTAINTY** | +5% crit | +20% crit damage | FOCUS: every hit without a critical brings the next closer, to +15% | at the peak the next hit crits outright, then resets |
| **NATURE — OVERGROWTH** | 0.3% health a second | healing +20% | healing ceiling +50% | overheal at full health becomes shield, half of it |
| **SHADOW — AFTERIMAGE** | +12% under 30% health | a kill leaves a SHADE, spent by the next damaging skill for +25% | hold two | a shade-spent skill strikes again for half |
| **SPIRIT — HARMONY** | +6% rate | each skill's first use +20% | +2% rate per distinct skill used, to +8% | all four slots filled and used: every skill opens again |

**A set's Source is the philosophy of the equipment, not a requirement on the skills.** The absence of
the old matching bonus is asserted as the *ratio* each build gains over its own bare-handed self, because
two builds of different Sources never deal the same damage to begin with.

---

## ARCHITECTURE

### 23. New typed state

Exactly what the design names, and nothing else: `Champion.CurrentShield` · `Champion.Shades` ·
wave-local `mindFocus`, `impactPending`, `resonanceRate`, `harmonyCharge[]`, `activated` · and the
fifteen `SkillRules` fields.

### 24. Removed fields

The four dead `SkillShape` dials above, `WaveReplay._shieldUntil`, and `BattleEventKind.Shield`.
`ShieldInsteadOfDamage` was checked and **kept** — BANKED sets it and the fight reads it.

### 25. Abstractions deliberately not introduced

No generic buff runtime. No `Dictionary<string, object>` on `SkillDef`. No effect-scripting layer. No
parallel benchmark model — the balance bench uses the game's own `Archetypes.Compose`. No new event bus:
the three shield events joined the existing `BattleEvent` stream.

---

## PERSISTENCE

### 26. Migrations

Uses and levels are permanent, restored **first**, before any name-matching can fail. A purchase whose
name this build no longer knows is dropped and its level comes back unspent.

### 27. THIRST → SIPHON

Mapped explicitly, because losing a *variation* silently drops all three purchases beneath it. PARCH and
TRICKLE survive with it. `GREEDY → PUMP` is the only reinforcement mapped — both cards read *"Lifesteal
is 50% stronger"* word for word. **Twenty-four other renames are deliberately not mapped**: each changed
what the thing does, and handing a player the new rule because it sits in the old one's slot would be
worse than handing back the level.

### 28. Fixture results

Thirteen tests in `save_migration_test.cs`, written as the rows a real save carries rather than through
today's catalogue — a fixture built from today's names could never fail the way an old file does. All
green.

---

## VALIDATION

### 29. Liveness

202 cases across four depths. Five rules were written from the brief, wired, **measured dormant, and
re-authored rather than shipped** — INTERCEPT, CLEANUP, REMNANT, BALM and SPEND's clock. Details in
`PLAN.md` §4.

### 30. Mono-Source

All six build, compose to four skills on four distinct skills with the right slot shape, and fight in
every band.

### 31. Mixed

Seven skill/set pairings each move a channel somewhere, out of the general rules and not a special case.
The eighth is a **conflict**, asserted as one: HARMONY is worth exactly nothing to a hunter holding a
slot open.

### 32. Set-shape comparison

5+3, 4+4 and 3+3+2 all open the rungs they should; a spread shape reaches no capstone and a committed one
does.

### 33. Shield balance

The MACHINE ladder keeps more health than nothing, never absorbs more than a capped wave-local resource
can account for, and no shield survives the wave that made it — asserted across six waves of a real
expedition.

### 34. The suite

**1332 / 1332 green.** All gates green (including the new `check_skill_doc.py`). Boot reads, writes and
reads back what it wrote.

---

## REMAINING

### 35. Values that want a playtest, not another test

- **The two bench pressures (5 and 26).** They separate "a wave you are beating" from "a wave that is
  beating you", which is the axis defensive branches live on — but where a real player sits between them
  at each depth is not something a bench can tell you.
- **SHRIVEL's 25%/−90%.** Deepened until it led somewhere; whether it now leads *too* far at high
  pressure is a question for hands on the game.
- **FINISH's execute, now decided after the blow.** It fires far more often than it did. That is the fix,
  but "far more often" is the kind of change that only feels right or wrong in play.
- **MOMENTUM's 25% beat floor.** The audit proves the basic attack survives the hungriest pair of
  Actives; whether an IMPACT every fourth beat *feels* like a capstone is a different question.

### 36. Deferred debt

- **§90's presentation tests** are covered where Core can reach — the replay's running total, the events,
  the set-ladder copy, the Source matrix behind every skill card. The MonoGame half (the strip appears and
  disappears, the VFX map) is checked by capture rather than by test, because the brief forbids MonoGame
  tests in Core and there is no game-side test project.
- **The `fightshield` fixture is one pose.** It photographs a full shield; a broken one, and a bite
  splitting between shield and health, still have no capture mode — and the lesson of this whole rework is
  that a state nothing can pose is a state nobody has looked at.
- **Carried from UX-V2**: the 150% vertical pass, mouse quantisation at every entry point, three owed
  fight fixtures, the orphaned `Warren.Name`.
