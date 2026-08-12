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
