# Game Flow

*Created: 2026-08-12*
*Status: Drafting — sections 1–3.3 written, 3.4 onward pending*

> **This document supersedes the end-to-end loop described in `game-concept.md`,
> `combat-encounter-system.md` and `expedition-auto-battle.md`.** Those describe a
> weak-point/telegraph action game that has zero implementation in `src/` and is no
> longer the design. See `design/system-audit-2026-08-12.md` for what the game
> actually is today.

---

## 1. Overview

Resonance Hunter is a **build laboratory with an automatic benchmark**. The player
assembles a Hunter — four woven skills, two permanent trees, eight gear slots — and
sends it down into a region. The descent runs itself: no targeting, no abilities to
fire, no retreat. It ends when the Hunter dies, and it reports how deep it got and
*why it stopped*. The player reads that report, changes the build, and descends
again.

Nothing is lost on death; the only cost is the time to descend again, and waves the
player has already proven are fast-forwarded so that cost stays small. Depth is the
score, and the reason a run ends is the game's core information.

The content is built to demand **different** builds rather than a bigger one. Every
ten waves the enemy composition and the active affix rotate, so a build tuned for
one band stalls in the next. Because both trees are on a hard point budget that can
never be completed, the player cannot answer every band at once — they specialise,
find their ceiling, and then decide what to give up in order to move it.

---

## 2. Player Fantasy

You are a strategist reading a machine you built.

The fantasy is not *"I dodged that"* — it is *"I know why I stopped at 23, and I
know what it costs to reach 30."* The satisfying moment is not a kill; it is the
moment a report tells you your damage is arriving in too many small pieces, you
trade a Projectile for a Trap and give up two mastery nodes to afford it, and the
next run ends at 31 for a completely different reason.

That loop should feel like tuning an engine, not like grinding. Every run is
information. A run that ends *earlier* than the last one is still useful if it ends
for a new reason.

The failure state to avoid is opacity. If the player cannot tell the difference
between "my build is wrong for this band" and "my numbers are just too small", the
game is a slot machine.

---

## 3. Detailed Rules

### 3.1 The expedition

An **expedition** is one descent into one region. It is the only place combat
happens and the only source of depth.

**Starting.** The player picks an unlocked region and descends. The build is
whatever is equipped at that moment; it cannot be changed until the expedition
ends.

**Descending.** The Hunter fights wave 1, then 2, then 3, and so on without limit.
Each wave is an enemy composition (§3.2). Clearing a wave pays its haul immediately
— haul is never at risk. Every 5th wave is a boss wave.

**No input.** There is no targeting, no ability button, no dodge, no retreat and no
pause. The only control on the screen is the speed selector, which multiplies the
playback rate of an already-decided result.

**Health is the run's budget.** The Hunter starts each expedition at full health
and **does not regenerate between waves**. Damage decides how *fast* the Hunter
clears; health decides how *deep* it gets. Sustain — leech, regeneration,
mitigation — is therefore a build axis with real weight rather than a rounding
error, and it is the axis a pure-damage build must consciously refuse.

**Ending.** An expedition ends when the Hunter's health reaches zero, or when a
wave cannot be cleared inside the wave tick ceiling (a *stall*). Both produce the
same post-run report (§3.4); a stall names the wall explicitly rather than letting
the player wonder why the numbers stopped moving.

**Cost of failure: time only.** Nothing is lost. Depth records, tree points,
currency, gear and unlocks all persist. The expedition simply ends.

**Fast-forward.** On re-entry to a region, every wave up to `bestDepth − 5` resolves
instantly: no animation, full haul paid, health attrition applied. Live playback
resumes for the last five proven waves and everything beyond. This is what keeps
"time only" an honest cost — without it, the price of testing a build change would
grow with progress until iteration stopped being worth it.

> **Design note.** Fast-forward is not a convenience feature. In a game with no
> in-run decisions, the loop *is* build → run → report → build, and the run is the
> slowest link. Anything that lengthens it without adding information is taxing the
> only thing the player is actually doing.

### 3.2 Wave composition and enemy archetypes

A wave is not one sprite. It is a **composition**: one to five creatures drawn from
the region's roster, each carrying a Source and an **archetype**.

The archetype is the load-bearing idea of the whole design. With combat automatic,
the only way a build can be *wrong* rather than merely *small* is if different
content punishes different build shapes. Four archetypes cover the four ways a
build can be shaped:

| Archetype | Composition | Punishes | Answered by |
|---|---|---|---|
| **Swarm** | 3–5 creatures, low health each, high combined damage | single-target skills, long cooldowns — you lose on action economy | multi-target Forms (Aura, Projectile), cooldown reduction, cleave |
| **Armoured** | 1–2 creatures, high Defense, moderate health | many small hits — flat mitigation eats each one | few large hits (Strike, Trap), armour-break, Mark amplification |
| **Caster** | 1–2 creatures, low health, very high damage on a long windup | slow kill speed — every extra second is a full hit taken | burst, interrupt triggers, Mark windows |
| **Bruiser** | 1 creature, high health *and* damage | nothing in particular — it is the honest DPS-and-sustain check | raw scaling, leech, mitigation |

**Armour must be flat, not multiplicative.** Enemy Defense currently reduces damage
by `100/(100+Defense)`, which scales every hit by the same factor and is therefore
completely indifferent to how many hits you land. Under that formula the Armoured
archetype cannot exist. Defense becomes a **flat subtraction per hit**, floored so a
hit always does something:

    hitDamage = max(rawHit * minFraction, rawHit - enemyDefense)

Ten hits of 40 against Defense 25 deliver 150; one hit of 400 delivers 375. That
difference is the archetype.

**Swarm must cost action economy.** A skill that hits one target spends its whole
cooldown killing one of five creatures while the other four keep attacking. Forms
therefore carry an explicit target count, and multi-target is a property a build
buys rather than something it always has.

**Sources still matter, but stop being free.** A composition draws creatures of
*mixed* Sources, so no single Source choice is strong against a whole wave. Picking
a Source is a bet on the region's weighting, not a lookup with one correct answer.

**Parts.** Armoured creatures carry a breakable plate. Enough damage concentrated
on it removes the flat mitigation for the rest of the fight and pays a part-break
reward (§3.5). Whether a build breaks parts is a consequence of its damage shape —
large hits break plates, small hits do not — so part-break income is another thing
a build either has or does not.

### 3.3 Depth bands and affix rotation

Depth is divided into **bands of ten waves**. A band sets two things: which
archetypes are common, and which **affix** is active.

Bands rotate on a fixed, authored cycle per region. The cycle is *learnable* — the
same region always presents the same ladder — because the player cannot react
inside a run and so must be able to plan before one.

An example region ladder:

| Band | Waves | Composition weighting | Affix |
|---|---|---|---|
| 1 | 1–10 | mixed, nothing dominant | none |
| 2 | 11–20 | Swarm-heavy | **Numbers** — every wave spawns one extra creature |
| 3 | 21–30 | Armoured-heavy | **Plated** — enemy Defense +50% |
| 4 | 31–40 | Caster-heavy | **Ritual** — enemy damage grows each wave within the band |
| 5 | 41–50 | Bruiser | **Endless** — leech and regeneration are halved |
| 6+ | 51+ | cycle repeats with a scaling multiplier | as above |

This is the structure that gives a scarce tree its meaning. A build that answers
Swarm perfectly walks band 2 and stops in band 3; the report says so; the player
decides whether to buy an answer to armour and what to give up for it. The ceiling
moves because the player made a trade, not because a number went up.

**Bands are not a difficulty curve.** They are a rotation of *demands*. Raw enemy
scaling continues underneath them, but the wall a player hits should almost always
be a demand they have not answered rather than a number they have not reached —
because only the first kind tells them what to do next.

### 3.4 The post-run report

With no in-run decisions, the report is the only moment the player can learn
anything. It is the most important screen in the game and should be designed
before the combat visuals, not after.

**It gives a diagnosis, never a prescription.** It tells the player *which demand
beat them* and shows the measurements that prove it. It never says which node to
buy. That line is what keeps theorycrafting in the player's hands while still
making the game teachable — a report that says "take Heavy Strike" has played the
game for them.

The report shows:

1. **Depth reached**, and whether it is a record for this region.
2. **The wall** — the wave that ended the run, its composition, the archetypes in
   it and the active affix. Named plainly: *"Wave 27 · Armoured x2 · PLATED."*
3. **The measurements**, taken over the last band only, because that is where the
   run actually failed:
   - damage delivered vs. damage absorbed by enemy mitigation
   - average hit size and hits per second
   - targets struck per skill activation, against creatures present
   - time-to-kill per creature vs. incoming damage per second
   - health lost per wave (the attrition curve)
4. **Haul** earned and **unlocks** crossed.
5. **What changed since the last run in this region** — the same measurements,
   diffed. This is what makes iteration legible: the player sees that their change
   raised average hit size by 60% and their wall moved from Armoured to Swarm.

**Every number in the report must map to something the player can change.** That
is the acceptance test for adding one. "61% of your damage was absorbed" points at
armour. "1.2 targets struck per cast against 4 creatures" points at action economy.
"22% health lost per wave" points at sustain. A number that does not point at a
lever is decoration and belongs somewhere else.

### 3.5 What an expedition pays

Three different things come out of a descent, and they deliberately buy three
different layers.

**Haul — pays gear.** Gleam and materials, credited per wave, never at risk. Boss
waves add a chest. This is the fast, continuous currency.

**Mastery points — pay the build tree.** One point per **first-time** depth
milestone (every 5 waves) in each region. Only the first time: farming a depth you
have already reached pays haul but no points. Pushing deeper is the only way to
earn them.

**Trait points — pay the permanent tree.** Rare and lumpy: region conquest,
corruption tiers, and mastery goals (§3.9). A player earns a handful across the
whole game, not a stream.

**Part-breaks** pay materials and raise the chest grade of the wave that produced
them. Whether a build breaks parts at all is a consequence of its damage shape
(§3.2), so this is income a build either has access to or does not — another
reason for large-hit builds to exist.

**The Warren pays neither kind of point.** It produces Gleam and materials only.
This is Pillar 3 in its revised form: idle buys gear, play buys identity. A player
who leaves the game running overnight comes back richer, not further along the
tree.

### 3.6 The two trees

The audit found three permanent trees all multiplying the same five numbers, with
the mastery tree handing out for free the triggers the Dust tree sold at a
permanent cost. The fix is not to merge them but to give them genuinely different
jobs.

**Mastery tree — scarce but fluid.** Buys how skills *behave*: target counts, hit
sizes, cooldowns, triggers, affinity. Points come from depth. **Respec is free and
instant.** The scarcity bites not because a choice is permanent but because the
player cannot have everything *at once* — so the tree is where they answer the band
in front of them. Re-solving it after a wall is the main activity of the game, and
charging for that would tax the thing the player is supposed to be doing.

**Trait tree — scarce and permanent.** Buys who the Hunter *is*: keystones,
structural unlocks, new build axes. Points come from conquests and goals.
**No respec.** Keystones sit at branch ends so reaching one costs the path to
another. These are the decisions the player lives with.

Two rules govern both:

- **Neither tree may be completed.** The point budget is authored so that clearing
  all current content reaches roughly a third of the nodes. When content is added,
  points and nodes grow together — the fraction is the invariant, not the count.
- **A node must change a shape, not just a number.** The audit found 31 of 43
  mastery nodes were flat percentage bonuses; a tree of those is a shopping list
  with a progress bar. Nodes change target counts, hit sizes, cooldowns, triggers
  and thresholds. Flat nodes exist only as connective tissue between real ones and
  are capped as a share of the tree.

### 3.7 Gear and the Forge

Gear is the third layer and the one idle time buys. It supplies the raw scaling
numbers plus, on each piece, one **trait** (a material property) and — on Rare and
above — one **enchant** (a behaviour trigger).

Three fixes the audit makes non-optional:

- **`PowerRating` must read what it ranks.** It currently reads a weapon
  multiplier, Defense and Health, and is blind to every trait and enchant — which
  means the EQUIP BEST button confidently equips the wrong item. Either it learns
  the whole item, or the button goes.
- **Rarity needs a sink.** It is a full build axis that is computed all the way
  through the simulation and then read by nothing. Rarity determines affix count
  and enchant availability.
- **Traits must interact with build shape.** HEAVY trading skill rate for hit size
  is a good answer to Armoured and a bad one against Swarm. That is the whole point
  of having traits; today they are flavour text on a number.

The Forge spends materials — the Warren's output — on upgrade (item level), reforge
(reroll the trait) and merge (combine duplicates). This is the loop that turns idle
time into build capability without letting idle time buy a decision.

---

## 4. Formulas

*Pending — to be written after §3 is settled.*

## 5. Edge Cases

*Pending.*

## 6. Dependencies

*Pending.*

## 7. Tuning Knobs

*Pending.*

## 8. Acceptance Criteria

*Pending.*
