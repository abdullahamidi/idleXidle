# COMBAT & CONTENT V2 — plan

Brief: `BRIEF.md` beside this file (the owner's text, verbatim). This is the working plan and the
running defects register.

---

## 1. What §0's inspection found

Read on 2026-09-01 at `4269437`, before any change.

| Thing | Where | State |
|---|---|---|
| Styles / skills / variations | `Core/Builds/SkillCatalogue.cs` (655 lines) | 6 styles, 12 skills, 24 variations, 3 reinforcements each — the structure §1 says to keep is **already correct**. |
| Skill parameters | `SkillDef` record | **~40 dials on one record.** Variations and reinforcements are `Func<SkillDef,SkillDef>` deltas applied in order. |
| Shared modifiers | `Core/Builds/SkillShape.cs` (~70 init-only fields) | The bag gear, sets, keystones, mastery and vows all combine into. `SkillShape.Combine`. |
| Simulation | `Core/Builds/SoloBattle.cs` (2081 lines) | One deterministic `ResolveWave` loop stepping `TickMs`. Locals hold per-wave state. |
| Shield today | a `bankedShield` **local float** in `ResolveWave` | Not state, not exposed, not capped, not reset-documented. Exactly what §7 forbids. |
| Bite path | `SoloBattle.cs:1719–1903` | Order today: mitigation → REPAY banks `taken` → `bankedShield` absorbs → Health. |
| IRON | `SoloBattle.cs:1863–1870` | Deducts Health first and **refunds** it after. Cannot compose with a second preventer. |
| Events | `BattleEvent(Kind, Slot, Amount, AtMs, FromSkill)` — `Expeditions/WaveModel.cs` | Flat struct; `BattleEventKind` already has a `Shield` member (payload = a banked health figure). |
| Battle state for presentation | `Champion` (`MaxHealth`, `Health`, `ElapsedMs`, `UndyingSpent`, `BeatCount`, `ReadyAt`) | The snapshot §19 needs a Shield field on. |
| Wave telemetry | `WaveMetrics` (`RawDamage`, `DeliveredDamage`, `Hits`, `HealthLost`, …) | Has no shield/prevention counters. |
| Sets | `Core/Economy/ElementSets.cs` (138 lines) | 2p/4p are `SourceBonus` matching (+8% each) — the model §49 deletes. 3p a stat, 5p a rule. All expressed as `SkillShape`. |
| Skill progression | `Core/Builds/SkillProgress.cs` | `_uses` / `_variation` / `_taken`. Level = ⌊√uses/2⌋, max 4. |
| Save | `SavedSkillProgress { SkillId, Uses, Variation, Reinforcements }` | Flat, by name. Migration hooks land here. |

### The three findings that shape the work

1. **The structure is right; the content is not.** §1's list is already true in the repository. This
   pass changes what the twelve skills SAY, not how a skill is modelled.
2. **`bankedShield` is a local, so Shield cannot be state.** Every §8–§20 requirement — cap, reset,
   snapshot exposure, ordering against prevention, exclusion from REPAY — needs Shield lifted onto
   `Champion` first. **C1 is therefore the foundation and goes first.**
3. **The bite path resolves in the wrong order for §12 and §14.** REPAY banks pre-shield damage
   (§14 forbids), and IRON refunds after deducting rather than preventing before (§12 forbids, and it
   is why MACHINE 5p could not compose). Both are in the same forty lines.

### The architecture decision this pass makes, and why

§79 says not to reintroduce a giant `SkillDef`; the repository still has one. §1 says this is not an
architecture rewrite, and §80 blesses small typed states.

**Decision.** Keep the dial-delta model for skill PARAMETERS — it is deterministic, it composes by
construction, and replacing it is the rewrite §1 forbids. Add new mechanics as **typed combat state
with a named owner, reset rule and consumer** (§80), not as more booleans on `SkillDef`. Where a skill
must declare it participates in a mechanic, prefer one typed field over several flags. No scripting
runtime, no `Dictionary<string,object>` (§80).

---

## 2. Checkpoints

Each is: build · unit tests · gates (`tools/check_all.sh`) · boot (`tools/check_boot.sh`) · commit ·
push. Nothing is called done on a compile.

### C1 — SHIELD, end to end (§7–§20, §86) — **DONE**
`Champion.CurrentShield` / `MaxShield` · 50% cap · wave-start reset · the bite path re-ordered so
prevention precedes absorption and absorption precedes Health · REPAY reads Health damage only ·
`ShieldGained` / `ShieldAbsorbed` / `ShieldBroken` events · `WaveMetrics` gains attempted / prevented /
absorbed / health-damage · IRON rewritten as prevention rather than refund. 20 tests from §86.

### C2 — The catalogue (§27–§48, §65, §88)
The Source matrix as an invariant test FIRST, then the 24 variations and their reinforcements. New
mechanics needed by the content: skill overkill carry, execute overkill, SPEND charges, INTERCEPT
targeting, SPLAY/CLUSTER retargeting, FLOOD, REMNANT, RIPE, ANCHOR, BALANCE, TRAIL. Liveness test per
reinforcement (§65) — 72 of them.

### C3 — The six sets (§49–§62, §87)
Delete matching-Source bonuses. Six ladders with typed state: `MindFocus`, `ShadeCount`,
`BodyImpactPending`, Spirit activation mask + Harmony charges, Shield (from C1). Per-tier tests and the
5+3 / 4+4 / 3+3+2 comparison.

### C4 — Persistence (§73–§78, §89)
THIRST → SIPHON · renamed-reinforcement mapping · unmapped purchases refunded as unspent levels ·
Source reassignment must not force a respec · dead-field sweep. Old-save fixtures.

### C5 — Presentation (§21–§26, §69–§72, §90)
Hunt HUD shield bar · shield VFX (persistent rim, gain, absorb, break) · Expedition Log
`SHIELD ABSORBED` · skill-tree Sources · set-ladder UI · one canonical SHIELD word and glyph.

### C6 — Balance (§63, §64, §66, §67, §68, §83, §84)
Mono-Source builds ×6 · mixed builds ×8 · variation balance · beat budget audit · Shield balance ·
set-shape comparison, all against the repository's real bands.

### C7 — Documentation and the §92 report
One current truth in `design/gdd/`; stale claims deleted; `REPORT.md` written.

---

## 3. Defects register

*Opened as they are found; each closes with the checkpoint that fixes it or is carried with an owner.*

- ~~REPAY banks pre-shield damage~~ **CLOSED C1** — the bank now takes `healthDamage`, computed after absorption.
- ~~IRON refunds instead of preventing~~ **CLOSED C1** — prevention is decided before the pool or the
  shield is touched, so a second preventer can see the bite is already gone and keep its charge.
- ~~`bankedShield` is uncapped and undocumented~~ **CLOSED C1** — `Champion.CurrentShield` / `MaxShield`,
  one cap, one wave reset, one grant door.
- ~~`BattleEventKind.Shield`'s payload is a banked health figure~~ **CLOSED C1** — replaced by
  `ShieldGained` / `ShieldAbsorbed` / `ShieldBroken`; `WaveReplay` tracks `CurrentShield` / `MaxShield`.
- **Sets are matching-Source damage** — `ElementSets.cs`. §49. → C3.
- **`SkillDef.ShieldInsteadOfDamage`** becomes a typed grant in C1 and its old dial is swept in C4. §78.

---

## 4. C2 — what the fight actually consumed

**Done.** Twelve skills re-authored against the Source matrix (§27–§48), 24 variations, 72
reinforcements, all live at four depths. `SkillRules` carries the fifteen typed rules the sim reads;
`SkillDef` gained no loose booleans (§80).

### Five rules were written, measured, and found structurally inert — re-authored rather than shipped

A reinforcement that sets a field the battle never consumes is the exact failure §65 exists to catch.
Each of these was proved dormant by measurement, not suspected:

| Rule as first written | Why the model cannot express it | What it became |
|---|---|---|
| **INTERCEPT** — "the stun is timed to the enemy attacking soonest" | There is ONE wave-wide bite clock, not per-enemy timers. Pushing it back a second costs the wave that second whenever the push happens, so timing changed no number at all. | The bite is **prevented**, not delayed: `nextBite` advances a whole interval. That damage never arrives. |
| **CLEANUP** — "arrows left over by a corpse re-aim" | SPLAY reaches the whole wave, one arrow each. It has no leftover arrow, and no aiming order that matters. (The retarget IS live on CLUSTER, which fires five per target — PUNCH THROUGH keeps it.) | A finisher: an enemy under half health takes the arrow 60% harder. |
| **REMNANT** — "the dead still count toward the slow" | The mire's grip is a running maximum, and living + dead is the number the wave opened with. The maximum was already holding that depth. | The dead drag on the survivors instead: the wave bites 5% softer per enemy lost, floored at −40%. |
| **BALM** — "1.5% a pulse instead of 1%" | The per-wave heal ceiling saturates at 1%, so both healed the identical number. | The pulse AND the room: the wave's healing ceiling rises 40% alongside it. |
| **SPEND**'s 6 s clock beside its hit count | Barely three damaging hits fall inside six seconds, so "3 hits" and "5 hits" were the same window and COUNT bought nothing. | The count is the whole rule. It holds until it is spent — which is also the honest shape for a hunter who does not choose when anything fires. |

### Two model conflicts, translated honestly rather than faked

- **Crit is an expected value** (`critFactor`), not a rolled event, so PERFECT CLAUSE cannot branch on
  "was this a crit". It spends a charge at the rate a hit is *not* one — which makes crit chance feed
  SPEND, the interaction the reinforcement is for.
- **One bite clock** — see INTERCEPT above.

### Defects closed in C2

- ~~SPEND charges skipped the basic attack~~ — a swing is a damaging hit; it was taking the
  amplification free and never spending a charge, which is what made COUNT unmeasurable.
- ~~`ampFrontFull` declared and never assigned~~ — ANCHOR was dormant. BRAND is a field, so the front
  enemy's deeper mark is armed on the tick, not on a cast.
- ~~`deadThisWave` counted once per SKILL per kill~~ — a four-skill build buried four dead for every
  creature that fell.
- ~~`BlowVerticalSliceTest` pinned TOLL at +50%~~ — the FLATTEN ladder is TOLL / BREAKTHROUGH / TRAIL
  and TOLL is +40%.

### The fixture was reporting on itself — two depths added

`ReinforcementLivenessTests` ran at 260 and 620 with the champion at half its pool. Under that fixture
RIPE (needs ≥90% health), BALM (needs to be hurt), and REMNANT / BALANCE / CLEANUP / PUNCH THROUGH (need
enemies that die mid-wave) could not register — the fixture's answer, not the content's. It now runs
four depths: a **90** where a healthy hunter beats the wave (and pairs with SPRAY, so a rule counted in
hits sees hits), and a **1600** where the wave wins the exchange and a bigger heal is a bigger heal.

**Green:** 1285/1285 unit · all gates · boot reads, writes, and reads back.

---

## 5. C3 — the six ladders

**Done.** `ElementSets` is re-authored end to end and every rung is measured on the sim the game runs.

### The matching-Source bonus is gone, and its absence is a test

The old ladder paid "+8% to your matching-Source skills" at two pieces and again at four. That was
paying the player twice for one decision — Source already decides the variation they may take, the
matchup they fight into and the Vows they may swear — so it is deleted and **not** replaced by a bigger
number. `test_a_set_never_pays_a_build_for_matching_its_source` asserts the absence two ways: no tier
carries a `SourceBonus`, and the same five SHADOW pieces are worth the same *ratio* to a SHADOW build
and a BODY one. (Not the same damage — two builds of different Sources fight different matchups before
any gear is worn. Asserting the raw totals was the fixture's first, wrong answer.)

### The six

| Set | 2p | 3p | 4p | 5p |
|---|---|---|---|---|
| **BODY — MOMENTUM** | +10% health | swing +20% | 35% of a direct hit's overkill carries | after a skill the next swing is an IMPACT: +75% and it carries all its waste |
| **MACHINE — PLATING** | −4 a bite | wave opens with 12% health as shield | −10% while shielded | once a wave the first bite that would hurt is stopped and becomes shield |
| **MIND — CERTAINTY** | +5% crit | +20% crit damage | FOCUS: every hit without a critical brings the next closer, to +15% | at the peak the next hit crits outright, then resets |
| **NATURE — OVERGROWTH** | 0.3% health a second | healing +20% | healing ceiling +50% | overheal at full health becomes shield, half of it |
| **SHADOW — AFTERIMAGE** | +12% under 30% health | a kill leaves a SHADE, spent by the next damaging skill for +25% | hold two | a shade-spent skill strikes again for half |
| **SPIRIT — HARMONY** | +6% rate | each skill's first use +20% | +2% rate per distinct skill used, to +8% | all four slots filled and used: every skill opens again |

### Typed state added — exactly the five the design names

`Champion.Shades` (persists between waves, per §56) plus wave-local `mindFocus`, `impactPending`,
`resonanceRate` / `harmonyCharge[]` / `activated`. Shield came in C1. No generic buff runtime, no
property bag.

### MIND met the same crit conflict as PERFECT CLAUSE

A critical here is an expected value, so "a critical resets FOCUS" cannot be a branch. FOCUS banks the
**non-crit share** of every hit — one whole hit at zero crit chance, a quarter at seventy-five percent —
which is the expectation of the written rule, is deterministic, and keeps the property the set sells: a
champion already critting climbs slowly. CERTAINTY is the one hit that is not an expectation; it empties
FOCUS, so it cannot chain.

### PLATING was briefly two dials, and that was a trap

Prevention and "the stopped bite becomes shield" were split into `PreventFirstDamagingBite` +
`ShieldFromPreventedBite`, which made a shape that prevents and grants nothing reachable — the two
shield tests found it immediately. Collapsed back into one rule.

### What the fixtures were measuring instead of the rung

Five of the six set tests failed first time on the fixture, not the content, and each is worth naming
because the same mistake is easy to repeat: the BODY carry does not clear MORE of a wave it always
cleared, it clears it SOONER; MACHINE's 4p is invisible in health because the 3p shield swallows every
bite whole, so it is read as absorbed-plus-health; NATURE's overflow needs a build that actually heals;
an AFTERIMAGE's proof is that the cast CADENCE is untouched, not that the cast count matches — a wave
that dies sooner casts a different number of times for reasons that have nothing to do with the rung.

**Green:** 1287/1287 unit · all gates · boot.

---

## 6. C4 — what happens to a save written before the rework

**Done.** The promise is narrow and absolute: **uses and levels are permanent.** What a level was spent
on may stop existing; the level itself never does. `SkillProgress.Restore` now writes the uses back
*first*, before any name-matching can fail, so every path below it only decides what the level has been
spent on.

### Two renames are mapped, and twenty-four deliberately are not

The rework renamed one variation and twenty-five reinforcements. Only two are mapped:

- **`THIRST` → `SIPHON`** (§74). A variation matters far more than a reinforcement, because losing one
  silently drops all three purchases beneath it as well. Same rule, same dial, clearer word — so PARCH
  and TRICKLE survive with it.
- **`GREEDY` → `PUMP`.** Both cards read *"Lifesteal is 50% stronger"* word for word.

Everything else that changed name also changed what it **does**. A player who bought "the window holds
4s instead of 2s" did not buy "a critical hit is empowered without spending one of them", and handing
them the second because it sits in the same position would be worse than handing back the level. Those
purchases are dropped and the levels return as unspent — which is what `Restore` already did for any
name it does not recognise.

### §75 — a Source change is not a respec

Eleven variations moved Source to satisfy the matrix. None changed name, and `Restore` matches by name,
so FINISH, CLUSTER, TORRENT, SUP and SHRIVEL all keep their selection. Pinned as a theory.

### §77 — the shield audit, not the shield assumption

`SaveGame` carries no champion runtime state at all: no health, no shield, no shades, no beat count. A
save records the **run**, not the instant, and `Champion` is constructed fresh for every wave. Nothing
to remove and nothing to add. `Champion.Shades` persists between waves inside a session (§56) and does
not survive a reload, which is honest for a run-scoped resource.

### §78 — four dead dials, swept

A script over `SkillShape` asked which dials the **game** never sets — a dial only a test produces is
a dial the player can never own. Four, and all four are the residue of C3's deleted set rungs:

| Dial | Was | Now |
|---|---|---|
| `SourceBonus` | the matching-Source 2p/4p rungs | deleted (§78 names it) |
| `Leech` | NATURE 5p | deleted |
| `FirstBiteFree` (FORTIFY) | MACHINE 5p | deleted |
| `CooldownRefundOnKillMs` | SHADOW 5p | deleted |

`ShieldInsteadOfDamage` was checked and **kept**: SNARE/BANKED sets it and `SoloBattle` grants through
the one shield door, so it has a live consumer.

The three shield tests that used FORTIFY as "a hard preventer that is not PLATING" now use **IRON** —
which is the preventer the brief itself names beside MACHINE's, and the only one a player can weave. The
composition being proved is now one someone can actually assemble.

**Green:** 1296/1296 unit · all gates · boot.

---

## 7. C5 — presentation

**Done.** SHIELD is now something the player can see, in one word, in one place, everywhere.

### The Hunt HUD

A **thin steel strip immediately above the pool**, never over it — an overlay would hide the number the
player is actually watching, and the brief's own test of the line is that "400 health and no shield"
must be one glance from "400 health and 200 shield". Not colour alone (§22): half the pool's height,
its own hard edge, vertical scoring across the fill so it reads as plates rather than a meter, and the
word and figure beside it. Drawn from the moment a run first grants shield and for the rest of it,
rather than only while some is standing — a strip that appears and vanishes as bites land is a flicker,
and a bar you only see full is a bar you cannot learn.

The **standing barrier** was an outline first, and the capture settled it: a 400×430 rectangle around a
person reads as a selection box in a level editor, and it enclosed a great deal of empty arena because
the champion's layout box is far larger than the champion. It is a held effect now (`VfxPlayer.Hold`,
the same infrastructure the aura uses) at about a third of the aura's size, resting low and breathing
slowly — SHIELD is a **state**, and a state that flashes is indistinguishable from an event.

**Gain says `+42 SHIELD`; absorption says nothing** (§24). Absorption happens on every bite a shielded
champion takes, and a figure on each would bury the health damage beside it — which is the number that
matters. **Breaking is loud**, because from the next bite the player is paying in health.

### The word collision §26 exists to prevent — found and fixed

`WaveReplay.IsShielded(slot)` was fed by the **UNDYING** event. The chip it drew said `SHIELDED` and
meant "undying is covering you", beside a bar that says SHIELD and means the resource — and UNDYING
already has its own chip two positions along, so the screen was saying one thing twice under two names.
It is `HasShield` now, meaning `CurrentShield > 0`, and the dead `_shieldUntil` window went with it.
The duplicate SHIELD chip in the status row went too: the strip says the word and the figure together,
and the status row is for the states that have no bar.

### Telemetry and the log

`RunReport` gained `ShieldAbsorbedFraction` and `ShieldAbsorbedPerWaveFraction`, carried through
`RunLog` and the save. The Expedition Log shows **SHIELD ABSORBED** beside HEALTH LOST — the two halves
of one question, what the wave landed and what it landed on — and never at zero, because a build with
no shield would otherwise read a row of nothing every run and learn to skip the rows. `AbsorbedFraction`
is untouched: that one is armour eating the champion's *outgoing* damage, a different question (§25).

### The set ladder

The capstone is **named on its rung** — a player talks about running MOMENTUM, not about their fifth
Body piece — and keeps its emphasis whether reached or not, because an unreached capstone is what the
ladder is *for*. Two fixture bugs fell out of photographing it: the right-hand label's room was a fixed
64 px, which truncated a rung's sentence when there was no label and was far too small for "WEARING THIS
REACHES IT" when there was; and the rules were cut rather than wrapped, so MACHINE's third rung ended
`…worth 12% of your maximum h…`. A rule the player cannot finish reading is a rung they cannot evaluate.

### `fightshield` — the fixture that made all of this visible

Every one of these states — the strip, the barrier, the callout, the chip — was a state **no capture
mode could pose**, and a state nothing can pose is a state nobody has ever looked at. `fightshield`
wears the full MACHINE set, so the 3-piece wave-start shield fills the bar from the wave's first frame
rather than depending on a grant landing in the instant the shutter opens.

**Green:** 1303/1303 unit · all gates · boot · photographed at 1280×720.

---

## 8. C6 — the bench, and the three things it caught

**Done.** `combat_balance_test.cs` runs against the repository's own archetypes — Swarm, Armoured,
Caster, Bruiser, composed by `Archetypes.Compose` (§83) — at two pressures, measuring every channel §84
asks for. The assertions are relationships and bounds, never pinned figures: a test that pins "BODY
deals 41,207" fails on every retune and teaches the next person to update the number rather than to
think about it.

### The bench was wrong three times before it was right, and every version accused the content

This is the same failure C2 hit, at a larger scale, and it is worth writing down because it will happen
again:

1. **The champion died in almost every band.** Six skills "failed" variation balance. Every one was the
   fixture: PIN's stun, NET's reflect, TEEMING's slow and SPEND's window all buy TIME, and time is worth
   nothing to someone who dies either way.
2. **The pool was then multiplied eight-fold to keep the champion alive** — which multiplied every
   percent-of-maximum-health effect eight-fold while the wave's damage stayed put. WILT's 1%-a-pulse heal
   became worth more than an attack break of ninety percent, and SUP "beat" SHRIVEL for a reason that
   existed only in the fixture. The fix was to lower the WAVE, keeping the ratios the game has.
3. **Counting band wins was the wrong question.** SIPHON heals and GLUT does not, so on a channel count
   SIPHON "won" every band on a free point while GLUT was quietly dealing 40% more damage in three of
   them. The rule is now the design's own: a branch is a real choice if there is **one** problem where it
   leads — "when would I choose this?" needs an answer, not a majority.

Also added: a second **pressure** (§83's "high incoming damage"). At one pressure a branch that buys
survival can only be compared on the damage axis, and it loses. SHRIVEL takes less than half the damage
SUP does — its break works exactly as designed — and still trailed everywhere, because at a gentle
pressure SUP's heal covers the whole difference. The answer to "when would I choose the cripple?" is
"when the wave is killing you", and a bench with no such wave cannot hear it.

### Three genuine content faults the corrected bench found

| Found | Why it was wrong | Fixed |
|---|---|---|
| **SPLAY beat CLUSTER at concentrating** (2833 vs 2408 on a Bruiser) | `MinimumHits` multiplied the raw, so SPLAY's surplus arrows landed as ONE hit paying armour once, while CLUSTER's five paid it five times. §37 says nothing may undermine CLUSTER's identity — and this was the branch built to spread beating the branch built to concentrate, at concentrating. | Surplus arrows are extra **hits**, and each pays the toll. |
| **FINISH's execute almost never fired** — identical damage to FLATTEN in two of four bands | It was asked *before* the blow, of the FRONT enemy only. A creature has to be under the line at the exact instant a six-beat skill comes round, and a wave whose creatures go from full to dead in two hits never passes through the window at all. | Asked **after** the blow lands, of the weakest living enemy — which is what "the blow kills a target under 15% health" says. It also finally gives CLEAN CUT something to carry: executing for exactly the health that was left produced no overkill at all, so §27's third purchase had nothing to buy. |
| **SHRIVEL led nowhere** | Structural: SUP keeps WILT's wave-wide break *and* heals; SHRIVEL only narrows and deepens. | Deepened to 25%/pulse and −90% — and the bench gained the pressure at which crippling beats healing. |

### The rest of §63–§68

- **§63** — all six mono-Source builds compose from their own variations, two Active and two Passive, on
  four distinct skills, and fight in every band. (Measured on a longer wave than the balance bench:
  SPIRIT's own Actives are five and six beats, and against a wave that ends in four neither comes round.)
- **§64** — seven skill/set pairings each move a channel somewhere, out of the general rules. The eighth
  is a **conflict**, asserted as one: HARMONY is worth exactly nothing to a hunter holding a slot open,
  with no branch anywhere excusing it.
- **§67** — the hungriest pair of Actives still leaves the basic attack a quarter of the beats, so
  MOMENTUM is buyable beside any of them.
- **§68** — the MACHINE ladder keeps more health than nothing and never absorbs more than a capped,
  wave-local resource can account for; and no shield survives the wave that made it, asserted across six
  waves of a real expedition.

**Green:** 1332/1332 unit · all gates · boot.

---

## 9. C7 — one current truth

**Done.** `production/audit/combat-v2/REPORT.md` answers §92 in its order.

### The design doc's skill tables are generated now

`design/gdd/skill-slots-and-skill-trees.md` §6 listed all twelve skills, twenty-four variations and
seventy-two reinforcements by hand — a second copy of the catalogue, and the copy that goes stale is
always the prose. It had SHEAR, SPUR, SEAL, GROUPING and eighteen other reinforcements the rework
replaced, THIRST for what is now SIPHON, and **no Source column at all** — so the six variations that
moved Source could not have been wrong in it, because it never said.

`tools/check_skill_doc.py` generates those tables from `SkillCatalogue` (with the Source column) and is
enrolled in `check_all.sh`, so the doc can never disagree with the fight again. `--write` regenerates it
after a catalogue change.

### Stale claims removed

- **`design/gdd/sources.md`** — the "+8% own-element skill damage at 2 and again at 4" ladder is gone,
  replaced by the six named capstones and by the rule the deletion is for: a set's Source is the
  philosophy of the equipment, not a requirement on the skills.
- **The "Names, audited" paragraph** listed renames between words that no longer exist (WAKE four times,
  MASS twice, DEEPEN twice, DREGS twice) — a record of collisions among deleted entries. Replaced by a
  pointer to the tests that check the property.

### `design/gdd/shield.md` — the page the mechanic never had

All eight required sections. The resolution order, the cap and reset, what shield is **not** (a bite it
ate is not damage taken; health-scaling reads health; it is not healing), every edge case, the tuning
knobs, and the twenty acceptance criteria mapped to `shield_test.cs`.

**Green:** 1332/1332 unit · all gates including the new one · boot.
